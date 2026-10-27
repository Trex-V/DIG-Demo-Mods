using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using DigDeliver;
using UnityEngine;

namespace ExtendedStats;

internal enum ItemMetric
{
    Broken,
    PickedUp,
    Crafted,
    Placed,
    Sold
}

internal enum StatsScope
{
    Player,
    World
}

internal sealed class BlockStatEntry
{
    internal string Key;
    internal string Name;
    internal Sprite Icon;
    internal long Broken;
    internal long PickedUp;
    internal long Crafted;
    internal long Placed;
    internal long Sold;
}

internal static class BlockStatsStore
{
    private sealed class MetricCounts
    {
        internal long Broken;
        internal long PickedUp;
        internal long Crafted;
        internal long Placed;
        internal long Sold;
    }

    private sealed class Counts
    {
        internal readonly MetricCounts World = new MetricCounts();
        internal readonly MetricCounts Player = new MetricCounts();
    }

    private static readonly Dictionary<string, Counts> Values = new Dictionary<string, Counts>();
    private static readonly string FilePath = Path.Combine(Paths.ConfigPath, "ExtendedStats.dat");
    private static ManualLogSource _log;

    internal static long Total(StatsScope scope)
    {
        // The dashboard's total is the sum of block-break counts across all stored entries.
        return Values.Values.Sum(counts => GetCountsForScope(counts, scope).Broken);
    }

    internal static void Load(ManualLogSource log)
    {
        // Read both current records and older formats so existing save data is preserved.
        _log = log;
        Values.Clear();

        if (!File.Exists(FilePath))
        {
            return;
        }

        try
        {
            foreach (string rawLine in File.ReadAllLines(FilePath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.Contains("|"))
                {
                    LoadDelimitedLine(line);
                }
                else
                {
                    LoadLegacyLine(line);
                }
            }
        }
        catch (Exception exception)
        {
            _log.LogError($"Could not load Extended Stats data from '{FilePath}': {exception}");
        }
    }

    internal static void RecordBroken(BlockId block, byte auxiliary, bool isLocalPlayer)
    {
        // Resolve this block to its drop item when possible before incrementing its broken total.
        string key = ResolveBrokenKey(block, auxiliary);
        Record(key, ItemMetric.Broken, 1, isLocalPlayer);
    }

    internal static void RecordItem(ushort itemId, ItemMetric metric, bool isLocalPlayer, long amount = 1)
    {
        // Ignore zero or negative deltas so stored totals can only increase.
        if (amount <= 0)
        {
            return;
        }

        Record(ItemKey(itemId), metric, amount, isLocalPlayer);
    }

    internal static List<BlockStatEntry> GetEntries(StatsScope scope)
    {
        // Migrate old keys, then include known game items so unused items also appear in the table.
        MigrateLegacyRecords();
        var keys = new HashSet<string>(Values.Keys, StringComparer.Ordinal);
        GameDatabase database = GameDatabase.Instance;
        if (database != null)
        {
            foreach (ItemDefinition item in database.items)
            {
                if (item != null)
                {
                    keys.Add(ItemKey(item.id));
                }
            }

            foreach (BlockDefinition block in database.blocks)
            {
                if (block != null)
                {
                    keys.Add(block.drop != null ? ItemKey(block.drop.id) : BlockKey(block.block));
                }
            }

            foreach (NodeDefinition node in database.nodes)
            {
                if (node != null)
                {
                    keys.Add(node.drop != null ? ItemKey(node.drop.id) : NodeKey(node.id));
                }
            }
        }

        return keys.Select(key => CreateEntry(key, scope))
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .ToList();
    }

    private static void LoadDelimitedLine(string line)
    {
        // Six-field records contain world totals only; eleven-field records also store player totals.
        string[] fields = line.Split('|');
        if (fields.Length == 6)
        {
            if (!TryParseMetricCounts(fields, 1, out MetricCounts world))
            {
                _log.LogWarning($"Ignoring invalid Extended Stats record: {line}");
                return;
            }

            CopyFrom(GetCounts(fields[0]).World, world);
            return;
        }

        if (fields.Length != 11
            || !TryParseMetricCounts(fields, 1, out MetricCounts worldCounts)
            || !TryParseMetricCounts(fields, 6, out MetricCounts playerCounts))
        {
            _log.LogWarning($"Ignoring invalid Extended Stats record: {line}");
            return;
        }

        Counts counts = GetCounts(fields[0]);
        CopyFrom(counts.World, worldCounts);
        CopyFrom(counts.Player, playerCounts);
    }

    private static bool TryParseMetricCounts(string[] fields, int start, out MetricCounts counts)
    {
        // Parse all five values together so a partial record cannot be loaded as valid data.
        counts = null;
        if (!TryParseCount(fields[start], out long broken)
            || !TryParseCount(fields[start + 1], out long pickedUp)
            || !TryParseCount(fields[start + 2], out long crafted)
            || !TryParseCount(fields[start + 3], out long placed)
            || !TryParseCount(fields[start + 4], out long sold))
        {
            return false;
        }

        counts = new MetricCounts
        {
            Broken = broken,
            PickedUp = pickedUp,
            Crafted = crafted,
            Placed = placed,
            Sold = sold
        };
        return true;
    }

    private static void LoadLegacyLine(string line)
    {
        // The oldest format tracked only broken blocks and nodes as "key=count".
        int separator = line.LastIndexOf('=');
        if (separator <= 0 || !TryParseCount(line.Substring(separator + 1), out long count))
        {
            _log.LogWarning($"Ignoring invalid legacy Extended Stats record: {line}");
            return;
        }

        string legacyKey = line.Substring(0, separator);
        if (!legacyKey.StartsWith("block:", StringComparison.Ordinal)
            && !legacyKey.StartsWith("node:", StringComparison.Ordinal))
        {
            _log.LogWarning($"Ignoring unknown legacy Extended Stats record: {line}");
            return;
        }

        GetCounts(legacyKey).World.Broken += count;
    }

    private static bool TryParseCount(string value, out long count)
    {
        // Use invariant numeric parsing and reject negative totals from malformed save data.
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) && count >= 0;
    }

    private static string ResolveBrokenKey(BlockId blockId, byte auxiliary)
    {
        // Group a broken block under its drop item when possible, otherwise keep its own identity.
        GameDatabase database = GameDatabase.Instance;
        if (database != null)
        {
            if (blockId == BlockId.Node)
            {
                NodeDefinition node = database.Node(auxiliary);
                if (node != null && node.drop != null)
                {
                    return ItemKey(node.drop.id);
                }

                return NodeKey(auxiliary);
            }

            BlockDefinition block = database.Block(blockId);
            if (block != null && block.drop != null)
            {
                return ItemKey(block.drop.id);
            }
        }

        return BlockKey(blockId);
    }

    private static void MigrateLegacyRecords()
    {
        // Once game definitions are available, fold old block/node totals into their drop-item rows.
        GameDatabase database = GameDatabase.Instance;
        if (database == null)
        {
            return;
        }

        var migratedKeys = new List<string>();
        foreach (KeyValuePair<string, Counts> pair in new List<KeyValuePair<string, Counts>>(Values))
        {
            string itemKey = null;
            if (TryReadId(pair.Key, "block:", out ushort blockId))
            {
                BlockDefinition block = database.Block((BlockId)blockId);
                if (block != null && block.drop != null)
                {
                    itemKey = ItemKey(block.drop.id);
                }
            }
            else if (TryReadId(pair.Key, "node:", out ushort nodeId))
            {
                NodeDefinition node = database.Node(nodeId);
                if (node != null && node.drop != null)
                {
                    itemKey = ItemKey(node.drop.id);
                }
            }

            if (itemKey != null)
            {
                Counts target = GetCounts(itemKey);
                Add(target.World, pair.Value.World);
                Add(target.Player, pair.Value.Player);
                migratedKeys.Add(pair.Key);
            }
        }

        if (migratedKeys.Count == 0)
        {
            return;
        }

        foreach (string key in migratedKeys)
        {
            Values.Remove(key);
        }

        Save();
    }

    private static BlockStatEntry CreateEntry(string key, StatsScope scope)
    {
        // Convert stable saved IDs into names and icons for the current game's UI.
        Counts counts = Values.TryGetValue(key, out Counts stored) ? stored : new Counts();
        MetricCounts values = GetCountsForScope(counts, scope);
        var entry = new BlockStatEntry
        {
            Key = key,
            Name = key,
            Broken = values.Broken,
            PickedUp = values.PickedUp,
            Crafted = values.Crafted,
            Placed = values.Placed,
            Sold = values.Sold
        };

        GameDatabase database = GameDatabase.Instance;
        if (database == null)
        {
            return entry;
        }

        if (TryReadId(key, "item:", out ushort itemId))
        {
            ItemDefinition item = database.Item(itemId);
            if (item != null)
            {
                entry.Name = item.Name;
                entry.Icon = item.icon;
            }
        }
        else if (TryReadId(key, "block:", out ushort blockId))
        {
            BlockDefinition block = database.Block((BlockId)blockId);
            if (block != null)
            {
                entry.Name = block.Name;
            }
        }
        else if (TryReadId(key, "node:", out ushort nodeId))
        {
            NodeDefinition node = database.Node(nodeId);
            if (node != null)
            {
                entry.Name = node.Name;
            }
        }

        return entry;
    }

    private static bool TryReadId(string key, string prefix, out ushort id)
    {
        id = 0;
        return key.StartsWith(prefix, StringComparison.Ordinal)
            && ushort.TryParse(key.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
    }

    private static string ItemKey(ushort id) => $"item:{id}";
    private static string BlockKey(BlockId id) => $"block:{(ushort)id}";
    private static string NodeKey(ushort id) => $"node:{id}";

    private static void Record(string key, ItemMetric metric, long amount, bool isLocalPlayer)
    {
        // World counts include every player; player counts include only the local player's actions.
        Counts counts = GetCounts(key);
        AddMetric(counts.World, metric, amount);
        if (isLocalPlayer)
        {
            AddMetric(counts.Player, metric, amount);
        }

        Save();
        ExtendedStatsWindow.RefreshIfOpen();
    }

    private static void AddMetric(MetricCounts counts, ItemMetric metric, long amount)
    {
        switch (metric)
        {
            case ItemMetric.Broken:
                counts.Broken += amount;
                break;
            case ItemMetric.PickedUp:
                counts.PickedUp += amount;
                break;
            case ItemMetric.Crafted:
                counts.Crafted += amount;
                break;
            case ItemMetric.Placed:
                counts.Placed += amount;
                break;
            case ItemMetric.Sold:
                counts.Sold += amount;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(metric), metric, null);
        }
    }

    private static MetricCounts GetCountsForScope(Counts counts, StatsScope scope)
    {
        return scope == StatsScope.Player ? counts.Player : counts.World;
    }

    private static Counts GetCounts(string key)
    {
        if (!Values.TryGetValue(key, out Counts counts))
        {
            counts = new Counts();
            Values.Add(key, counts);
        }

        return counts;
    }

    private static void Save()
    {
        // Write deterministic, invariant-culture records so the file is stable across locales.
        try
        {
            string directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string[] lines = Values
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => string.Join("|", new[]
                {
                    pair.Key,
                    pair.Value.World.Broken.ToString(CultureInfo.InvariantCulture),
                    pair.Value.World.PickedUp.ToString(CultureInfo.InvariantCulture),
                    pair.Value.World.Crafted.ToString(CultureInfo.InvariantCulture),
                    pair.Value.World.Placed.ToString(CultureInfo.InvariantCulture),
                    pair.Value.World.Sold.ToString(CultureInfo.InvariantCulture),
                    pair.Value.Player.Broken.ToString(CultureInfo.InvariantCulture),
                    pair.Value.Player.PickedUp.ToString(CultureInfo.InvariantCulture),
                    pair.Value.Player.Crafted.ToString(CultureInfo.InvariantCulture),
                    pair.Value.Player.Placed.ToString(CultureInfo.InvariantCulture),
                    pair.Value.Player.Sold.ToString(CultureInfo.InvariantCulture)
                }))
                .ToArray();
            File.WriteAllLines(FilePath, lines);
        }
        catch (Exception exception)
        {
            _log.LogError($"Could not save Extended Stats data to '{FilePath}': {exception}");
        }
    }

    private static void Add(MetricCounts target, MetricCounts source)
    {
        target.Broken += source.Broken;
        target.PickedUp += source.PickedUp;
        target.Crafted += source.Crafted;
        target.Placed += source.Placed;
        target.Sold += source.Sold;
    }

    private static void CopyFrom(MetricCounts target, MetricCounts source)
    {
        target.Broken = source.Broken;
        target.PickedUp = source.PickedUp;
        target.Crafted = source.Crafted;
        target.Placed = source.Placed;
        target.Sold = source.Sold;
    }
}
