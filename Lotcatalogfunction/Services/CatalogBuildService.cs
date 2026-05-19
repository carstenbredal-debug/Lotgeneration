using LotCatalogFunction.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LotCatalogFunction.Services
{
    public class CatalogBuildService
    {
        public List<CatalogLot> BuildCatalogLots(
            IEnumerable<GeneratedLot> lots,
            IEnumerable<StringDefinition> stringDefinitions,
            IEnumerable<LotGroupOrderFunction> groupOrders,
            IEnumerable<LotSortOrder> sortOrders,
            IEnumerable<CatalogNumberRule> catalogNumberRules)
        {
            var lotList = lots.ToList();

            var stringColumns = stringDefinitions
                .Where(x => x.IsActive)
                .Select(x => x.ColumnName)
                .ToList();

            var orderedColumns = groupOrders
                .OrderBy(x => x.GroupOrder)
                .Select(x => x.ColumnName)
                .ToList();

            var sortMap = sortOrders
                .GroupBy(x => BuildSortKey(x.ColumnName, x.Value))
                .ToDictionary(
                    x => x.Key,
                    x => x.First().SortOrder,
                    StringComparer.OrdinalIgnoreCase
                );

            var ruleMap = catalogNumberRules
                .Where(x => x.IsActive)
                .ToDictionary(
                    x => BuildRuleKey(x.SalesType, x.Gender, x.Group),
                    x => x.StartNumber,
                    StringComparer.OrdinalIgnoreCase
                );

            var strings = lotList
                .GroupBy(lot => BuildStringKey(lot, stringColumns))
                .Select(g => new CatalogStringGroup
                {
                    Representative = g.First(),
                    Lots = g.ToList()
                })
                .ToList();

            var sortedStrings = SortStrings(strings, orderedColumns, sortMap);

            var nextNumberByRule = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase
            );

            var catalogLots = new List<CatalogLot>();
            var catalogSortOrder = 1;

            foreach (var catalogString in sortedStrings)
            {
                var representative = catalogString.Representative;

                var ruleKey = BuildRuleKey(
                    representative.SalesType,
                    representative.Gender,
                    representative.Group
                );

                if (!ruleMap.TryGetValue(ruleKey, out var prefix))
                {
                    throw new InvalidOperationException(
                        $"Missing dbo.CatalogNumberRule for {ruleKey}"
                    );
                }

                if (!nextNumberByRule.ContainsKey(ruleKey))
                {
                    nextNumberByRule[ruleKey] = prefix * 1000 + 1;
                }

                var orderedLotsInString = catalogString.Lots
                    .OrderByDescending(x => x.IsShow == "Yes")
                    .ThenByDescending(x => x.TotalSkins)
                    .ThenByDescending(x => x.BoxCount)
                    .ThenBy(x => x.ShowlotBoxNumber)
                    .ToList();

                var stringNumber = nextNumberByRule[ruleKey];

                foreach (var lot in orderedLotsInString)
                {
                    var lotNumber = nextNumberByRule[ruleKey];

                    catalogLots.Add(new CatalogLot
                    {
                        LotUniqueID = lot.UniqueID,

                        StringNumber = stringNumber,
                        LotNumber = lotNumber,
                        CatalogSortOrder = catalogSortOrder++,

                        IsShow = lot.IsShow,

                        SalesType = lot.SalesType,
                        Gender = lot.Gender,
                        Group = lot.Group,
                        HairLength = lot.HairLength,
                        Size = lot.Size,
                        Quality = lot.Quality,
                        Color = lot.Color,
                        Clarity = lot.Clarity,
                        Damages = lot.Damages,

                        IncludedBoxNumbers = lot.IncludedBoxNumbers,
                        BoxCount = lot.BoxCount,
                        TotalSkins = lot.TotalSkins
                    });

                    nextNumberByRule[ruleKey]++;
                }
            }

            return catalogLots;
        }

        private static List<CatalogStringGroup> SortStrings(
            List<CatalogStringGroup> strings,
            List<string> orderedColumns,
            IReadOnlyDictionary<string, int> sortMap)
        {
            IOrderedEnumerable<CatalogStringGroup>? ordered = null;

            foreach (var columnName in orderedColumns)
            {
                Func<CatalogStringGroup, int> selector = item =>
                {
                    var lot = item.Representative;

                    var effectiveColumn =
                        columnName == "Size"
                            ? GetSizeColumnName(lot)
                            : columnName;

                    var value = GetValue(lot, columnName);

                    return GetSortRank(sortMap, effectiveColumn, value);
                };

                ordered = ordered == null
                    ? strings.OrderBy(selector)
                    : ordered.ThenBy(selector);
            }

            return (ordered ?? strings.OrderBy(x => 0)).ToList();
        }

        private static string BuildStringKey(
            GeneratedLot lot,
            List<string> stringColumns)
        {
            return string.Join("|",
                stringColumns.Select(column => GetValue(lot, column))
            );
        }

        private static string GetValue(
            GeneratedLot lot,
            string columnName)
        {
            return columnName switch
            {
                "SalesType" => lot.SalesType ?? "",
                "Gender" => lot.Gender ?? "",
                "Group" => lot.Group ?? "",
                "HairLength" => lot.HairLength ?? "",
                "Size" => lot.Size ?? "",
                "Quality" => lot.Quality ?? "",
                "Color" => lot.Color ?? "",
                "Clarity" => lot.Clarity ?? "",
                "Damages" => lot.Damages ?? "",

                _ => throw new InvalidOperationException(
                    $"Unknown catalog column: {columnName}"
                )
            };
        }

        private static int GetSortRank(
            IReadOnlyDictionary<string, int> sortMap,
            string columnName,
            string value)
        {
            var key = BuildSortKey(columnName, value);

            return sortMap.TryGetValue(key, out var rank)
                ? rank
                : int.MaxValue;
        }

        private static string BuildSortKey(
            string columnName,
            string value)
        {
            return $"{columnName?.Trim() ?? ""}|{value?.Trim() ?? ""}";
        }

        private static string BuildRuleKey(
            string salesType,
            string gender,
            string group)
        {
            return $"{salesType?.Trim() ?? ""}|{gender?.Trim() ?? ""}|{group?.Trim() ?? ""}";
        }

        private static string GetSizeColumnName(GeneratedLot lot)
        {
            return lot.Gender switch
            {
                "Females" => "Size_Females",
                "Males" => "Size_Males",
                _ => "Size"
            };
        }

        private sealed class CatalogStringGroup
        {
            public GeneratedLot Representative { get; set; } = new();
            public List<GeneratedLot> Lots { get; set; } = new();
        }
    }
}