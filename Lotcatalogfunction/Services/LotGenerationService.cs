using LotCatalogFunction.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LotCatalogFunction.Services
{
    public class LotGenerationService
    {
        public LotGenerationResult GenerateLots(
            IEnumerable<BoxRow> boxes,
            IEnumerable<LotSizeRule> rules,
            IEnumerable<LotGroupOrderFunction> groupOrders,
            IEnumerable<LotSortOrder> sortOrders)
        {
            var runId = Guid.NewGuid();
            var result = new LotGenerationResult { RunId = runId };

            var activeBoxes = boxes.ToList();
            var sortOrderList = sortOrders.ToList();

            var missingSortKeys = FindMissingSortMappings(activeBoxes, sortOrderList);

            var orderedColumns = groupOrders
                .OrderBy(x => x.GroupOrder)
                .Select(x => x.ColumnName)
                .ToList();

            var sortMap = sortOrderList
                .GroupBy(x => BuildSortKey(x.ColumnName, x.Value))
                .ToDictionary(
                    x => x.Key,
                    x => x.First().SortOrder,
                    StringComparer.OrdinalIgnoreCase
                );

            var groups = activeBoxes.GroupBy(box =>
                string.Join("|", orderedColumns.Select(col => GetGroupValue(box, col)))
            );

            foreach (var group in groups)
            {
                var groupBoxes = group.ToList();
                var representative = groupBoxes.First();

                var showlots = groupBoxes
                    .Where(x => x.BoxType == "Showlot")
                    .ToList();

                var groupMissingSortKeys = groupBoxes
                    .SelectMany(box => GetMissingSortKeysForBox(box, missingSortKeys))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (groupMissingSortKeys.Any())
                {
                    result.SkippedGroups.Add(
                        CreateSkippedGroup(
                            runId,
                            "Missing LotSortOrder: " + string.Join(", ", groupMissingSortKeys),
                            representative,
                            groupBoxes,
                            showlots.Count
                        )
                    );

                    continue;
                }

                if (showlots.Count == 0)
                {
                    result.SkippedGroups.Add(
                        CreateSkippedGroup(runId, "Missing showlot", representative, groupBoxes, showlots.Count)
                    );

                    continue;
                }

                if (showlots.Count > 1)
                {
                    result.SkippedGroups.Add(
                        CreateSkippedGroup(runId, "Multiple showlots", representative, groupBoxes, showlots.Count)
                    );

                    continue;
                }

                var rule = FindRule(
                    representative.Gender,
                    representative.Size,
                    rules
                );

                if (rule == null)
                {
                    result.SkippedGroups.Add(
                        CreateSkippedGroup(runId, "Missing size rule", representative, groupBoxes, showlots.Count)
                    );

                    continue;
                }

                var showlot = showlots.Single();

                var storageBoxes = SortBoxesInsideGroup(
                    groupBoxes.Where(x => x.BoxType != "Showlot"),
                    orderedColumns,
                    sortMap
                );

                var firstLotBoxes = new List<BoxRow>
                {
                    showlot
                };

                foreach (var box in storageBoxes.ToList())
                {
                    var storageBoxCount = firstLotBoxes.Count(x => x.BoxType != "Showlot");
                    var potentialBoxes = storageBoxCount + 1;
                    var potentialSkins = firstLotBoxes.Sum(x => x.Skins) + box.Skins;

                    if (
                        potentialBoxes <= rule.MaxBoxes
                        &&
                        potentialSkins <= rule.MaxLotSizeInclShowlot
                    )
                    {
                        firstLotBoxes.Add(box);
                        storageBoxes.Remove(box);
                    }

                    if (potentialBoxes >= rule.MaxBoxes)
                        break;
                }

                result.Lots.Add(
                    CreateLot(firstLotBoxes, "Yes", showlot.BoxNumber)
                );

                while (storageBoxes.Any())
                {
                    var lotBoxes = new List<BoxRow>();

                    foreach (var box in storageBoxes.ToList())
                    {
                        var potentialBoxes = lotBoxes.Count + 1;
                        var potentialSkins = lotBoxes.Sum(x => x.Skins) + box.Skins;

                        if (
                            potentialBoxes <= rule.MaxBoxes
                            &&
                            potentialSkins <= rule.MaxLotSizeExclShowlot
                        )
                        {
                            lotBoxes.Add(box);
                            storageBoxes.Remove(box);
                        }

                        if (lotBoxes.Count >= rule.MaxBoxes)
                            break;
                    }

                    if (!lotBoxes.Any())
                    {
                        lotBoxes.Add(storageBoxes.First());
                        storageBoxes.RemoveAt(0);
                    }

                    result.Lots.Add(
                        CreateLot(lotBoxes, "No", showlot.BoxNumber)
                    );
                }
            }

            return result;
        }

        private static List<BoxRow> SortBoxesInsideGroup(
            IEnumerable<BoxRow> boxes,
            List<string> orderedColumns,
            IReadOnlyDictionary<string, int> sortMap)
        {
            IOrderedEnumerable<BoxRow>? orderedBoxes = null;

            foreach (var columnName in orderedColumns)
            {
                Func<BoxRow, int> selector = box =>
                {
                    var effectiveColumnName =
                        columnName == "Size"
                            ? GetSizeColumnName(box)
                            : columnName;

                    var value = GetGroupValue(box, columnName);

                    return GetSortRank(sortMap, effectiveColumnName, value);
                };

                orderedBoxes = orderedBoxes == null
                    ? boxes.OrderBy(selector)
                    : orderedBoxes.ThenBy(selector);
            }

            return (orderedBoxes ?? boxes.OrderBy(x => x.BoxNumber))
                .ThenByDescending(x => x.Skins)
                .ThenBy(x => x.BoxNumber)
                .ToList();
        }

        private static HashSet<string> FindMissingSortMappings(
            IEnumerable<BoxRow> boxes,
            IEnumerable<LotSortOrder> sortOrders)
        {
            var lookup = sortOrders
                .Select(x => BuildSortKey(x.ColumnName, x.Value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var box in boxes)
            {
                CheckValue("SalesType", box.SalesType);
                CheckValue("Gender", box.Gender);
                CheckValue("Group", box.Group);
                CheckValue("HairLength", box.HairLength);
                CheckValue(GetSizeColumnName(box), box.Size);
                CheckValue("Quality", box.Quality);
                CheckValue("Color", box.Color);
                CheckValue("Clarity", box.Clarity);
                CheckValue("Damages", box.Damages);
            }

            return missing;

            void CheckValue(string columnName, string value)
            {
                var key = BuildSortKey(columnName, value);

                if (!lookup.Contains(key))
                    missing.Add(key);
            }
        }

        private static List<string> GetMissingSortKeysForBox(
            BoxRow box,
            HashSet<string> missingSortKeys)
        {
            var result = new List<string>();

            CheckValue("SalesType", box.SalesType);
            CheckValue("Gender", box.Gender);
            CheckValue("Group", box.Group);
            CheckValue("HairLength", box.HairLength);
            CheckValue(GetSizeColumnName(box), box.Size);
            CheckValue("Quality", box.Quality);
            CheckValue("Color", box.Color);
            CheckValue("Clarity", box.Clarity);
            CheckValue("Damages", box.Damages);

            return result;

            void CheckValue(string columnName, string value)
            {
                var key = BuildSortKey(columnName, value);

                if (missingSortKeys.Contains(key))
                    result.Add(key);
            }
        }

        private static string GetGroupValue(BoxRow box, string columnName)
        {
            return columnName switch
            {
                "SalesType" => box.SalesType ?? "",
                "Gender" => box.Gender ?? "",
                "Group" => box.Group ?? "",
                "HairLength" => box.HairLength ?? "",
                "Size" => box.Size ?? "",
                "Quality" => box.Quality ?? "",
                "Color" => box.Color ?? "",
                "Clarity" => box.Clarity ?? "",
                "Damages" => box.Damages ?? "",

                _ => throw new InvalidOperationException(
                    $"Unknown lot group order column: {columnName}"
                )
            };
        }

        private static int GetSortRank(
            IReadOnlyDictionary<string, int> sortMap,
            string columnName,
            string value)
        {
            var key = BuildSortKey(columnName, value);

            return sortMap.TryGetValue(key, out var sortOrder)
                ? sortOrder
                : int.MaxValue;
        }

        private static string BuildSortKey(string columnName, string value)
        {
            return $"{columnName?.Trim() ?? ""}|{value?.Trim() ?? ""}";
        }

        private static string GetSizeColumnName(BoxRow box)
        {
            return box.Gender switch
            {
                "Females" => "Size_Females",
                "Males" => "Size_Males",
                _ => "Size"
            };
        }

        private static LotSizeRule FindRule(
            string gender,
            string size,
            IEnumerable<LotSizeRule> rules)
        {
            return rules
                .Where(x =>
                    x.IsActive
                    &&
                    string.Equals(x.Gender, gender, StringComparison.OrdinalIgnoreCase)
                    &&
                    string.Equals(x.Size, size, StringComparison.OrdinalIgnoreCase)
                )
                .OrderBy(x => x.Priority)
                .FirstOrDefault();
        }

        private static GeneratedLot CreateLot(
            List<BoxRow> boxes,
            string isShow,
            int? showlotBoxNumber)
        {
            var first = boxes.First();

            return new GeneratedLot
            {
                UniqueID = Guid.NewGuid(),

                IsShow = isShow,
                ShowlotBoxNumber = showlotBoxNumber,

                SalesType = first.SalesType,
                Group = first.Group,
                Gender = first.Gender,
                Size = first.Size,
                Color = first.Color,
                Quality = first.Quality,
                Clarity = first.Clarity,
                HairLength = first.HairLength,
                Damages = first.Damages,

                IncludedBoxNumbers = string.Join(", ", boxes.Select(x => x.BoxNumber)),
                BoxCount = boxes.Count,
                TotalSkins = boxes.Sum(x => x.Skins)
            };
        }

        private static SkippedGroup CreateSkippedGroup(
            Guid runId,
            string reason,
            BoxRow representative,
            List<BoxRow> boxes,
            int showlotCount)
        {
            return new SkippedGroup
            {
                RunID = runId,
                Reason = reason,

                SalesType = representative.SalesType,
                Group = representative.Group,
                Gender = representative.Gender,
                Size = representative.Size,
                Color = representative.Color,
                Quality = representative.Quality,
                Clarity = representative.Clarity,
                HairLength = representative.HairLength,
                Damages = representative.Damages,

                BoxCount = boxes.Count,
                ShowlotCount = showlotCount,
                TotalSkins = boxes.Sum(x => x.Skins),
                BoxNumbers = string.Join(", ", boxes.Select(x => x.BoxNumber))
            };
        }
    }
}