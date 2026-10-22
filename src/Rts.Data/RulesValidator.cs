using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rts.Data.Yaml;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using YamlDotNet.RepresentationModel;

namespace Rts.Data;

/// <summary>
/// Checks the meaning of rules that have been read (docs/implementation/M3_base_economy.md, 4.3): numbers in their ranges, references to
/// definitions that exist and have the right kind, no cycle of prerequisites, and every faction with its construction yard, MCV, refinery
/// and harvester. Every mistake is collected, at the place where its value is written: an inherited field points into the template, an id
/// in a list points at that id. The schema itself (unknown keys, missing fields, values that do not parse) and inheritance cycles are found
/// earlier, while the files are read; the assets are checked by the client, since the simulation does not need them.
/// </summary>
internal static class RulesValidator
{
    /// <summary>The largest side of a building footprint, in cells.</summary>
    public const int MaxFootprint = 6;

    /// <param name="sources">The resolved entry of every definition in <paramref name="rules"/>, by id.</param>
    public static void Check(RulesDatabase rules, IReadOnlyDictionary<string, RawDefinition> sources, List<RulesError> errors)
    {
        var checker = new Checker(rules, sources, errors);
        foreach (var unit in rules.Units)
        {
            checker.Unit(unit);
        }

        foreach (var building in rules.Buildings)
        {
            checker.Building(building);
        }

        foreach (var weapon in rules.Weapons)
        {
            checker.Weapon(weapon);
        }

        foreach (var locomotor in rules.Locomotors)
        {
            checker.Locomotor(locomotor);
        }

        checker.Terrains();
        foreach (var faction in rules.Factions)
        {
            checker.Faction(faction);
        }

        checker.PrerequisiteCycles();
    }

    /// <summary>
    /// An error at the value of <paramref name="key"/> in the definition <paramref name="id"/>; at its id when the key is empty or not
    /// written; for the whole data, without a place, when the id is empty.
    /// </summary>
    public static RulesError At(IReadOnlyDictionary<string, RawDefinition> sources, string id, string key, string reason)
    {
        if (id.Length == 0)
        {
            return new RulesError("", 0, 0, reason);
        }

        var source = sources[id];
        return source.Fields.TryGetValue(key, out var field)
            ? RulesFormatException.At(field.FileName, field.Value, reason).Error
            : RulesFormatException.At(source.FileName, source.IdNode, reason).Error;
    }

    private sealed class Checker(RulesDatabase rules, IReadOnlyDictionary<string, RawDefinition> sources, List<RulesError> errors)
    {
        public void Unit(UnitDef unit)
        {
            Positive(unit.Id, "cost", unit.Cost);
            Positive(unit.Id, "hp", unit.Hp);
            Positive(unit.Id, "build_time", unit.BuildTime);
            NotNegative(unit.Id, "speed", unit.Speed);
            NotNegative(unit.Id, "turn_rate", unit.TurnRate);
            NotNegative(unit.Id, "sight", unit.Sight);
            FactionOf(unit.Id, unit.Faction);
            if (!rules.TryGetLocomotor(unit.Locomotor, out _))
            {
                Error(unit.Id, "locomotor", Missing(unit.Id, "locomotor", unit.Locomotor));
            }

            for (var i = 0; i < unit.Weapons.Length; i++)
            {
                if (!rules.TryGetWeapon(unit.Weapons[i], out _))
                {
                    ItemError(unit.Id, "weapons", i, Missing(unit.Id, "weapon", unit.Weapons[i]));
                }
            }

            Prerequisites(unit.Id, unit.Prerequisites);
        }

        public void Building(BuildingDef building)
        {
            Positive(building.Id, "cost", building.Cost);
            Positive(building.Id, "hp", building.Hp);
            Positive(building.Id, "build_time", building.BuildTime);
            NotNegative(building.Id, "build_up", building.BuildUp);
            var (width, height) = (building.Footprint.Width, building.Footprint.Height);
            if (width is < 1 or > MaxFootprint || height is < 1 or > MaxFootprint)
            {
                Error(
                    building.Id,
                    "footprint",
                    $"the footprint of '{building.Id}' must be 1 to {MaxFootprint} cells a side, not [{width}, {height}]");
            }

            if (building.ExitCell is { } exit && (exit.X < -1 || exit.X > width || exit.Y < -1 || exit.Y > height))
            {
                Error(
                    building.Id,
                    "exit_cell",
                    $"the exit cell [{exit.X}, {exit.Y}] of '{building.Id}' must be in its footprint or next to it");
            }

            FactionOf(building.Id, building.Faction);
            Prerequisites(building.Id, building.Prerequisites);
        }

        public void Weapon(WeaponDef weapon)
        {
            NotNegative(weapon.Id, "damage", weapon.Damage);
            NotNegative(weapon.Id, "reload", weapon.Reload);
            if (weapon.Range < Fixed.Zero)
            {
                Error(weapon.Id, "range", $"'range' of '{weapon.Id}' must not be negative");
            }
        }

        public void Locomotor(LocomotorDef locomotor)
        {
            if (locomotor.Slope < Fixed.Zero || locomotor.Slope > Fixed.One)
            {
                Error(locomotor.Id, "slope", $"'slope' of '{locomotor.Id}' must be 0% to 100%");
            }
        }

        /// <summary>Speeds for known locomotors, at least zero, one for every locomotor; and no land code used twice.</summary>
        public void Terrains()
        {
            var codes = new SortedDictionary<char, string>();
            foreach (var terrain in rules.Terrains)
            {
                foreach (var speed in terrain.Speed)
                {
                    if (!rules.TryGetLocomotor(speed.Locomotor, out _))
                    {
                        SpeedError(terrain.Id, speed.Locomotor, Missing(terrain.Id, "locomotor", speed.Locomotor));
                    }
                    else if (speed.Multiplier < Fixed.Zero)
                    {
                        SpeedError(terrain.Id, speed.Locomotor, $"the speed of '{speed.Locomotor}' on '{terrain.Id}' must not be negative");
                    }
                }

                foreach (var locomotor in rules.Locomotors)
                {
                    if (!terrain.Speed.Any(speed => speed.Locomotor == locomotor.Id))
                    {
                        Error(terrain.Id, "speed", $"'{terrain.Id}' has no speed for locomotor '{locomotor.Id}'");
                    }
                }

                if (!codes.TryAdd(terrain.Code, terrain.Id))
                {
                    Error(terrain.Id, "code", $"code '{terrain.Code}' of '{terrain.Id}' is already used by '{codes[terrain.Code]}'");
                }
            }
        }

        /// <summary>The faction's MCV and harvester are its units, its construction yard and refinery are its buildings.</summary>
        public void Faction(FactionDef faction)
        {
            UnitOf(faction.Id, "mcv", faction.Mcv);
            UnitOf(faction.Id, "harvester", faction.Harvester);
            BuildingOf(faction.Id, "construction_yard", faction.ConstructionYard);
            BuildingOf(faction.Id, "refinery", faction.Refinery);
        }

        /// <summary>
        /// Walks the prerequisites of every building, in id order. A prerequisite that leads back to a building whose walk is under way
        /// closes a cycle; it is reported there, with the cycle, once.
        /// </summary>
        public void PrerequisiteCycles()
        {
            var done = new SortedSet<string>(StringComparer.Ordinal);
            var path = new List<string>();
            foreach (var building in rules.Buildings)
            {
                Visit(building, done, path);
            }
        }

        private void Visit(BuildingDef building, SortedSet<string> done, List<string> path)
        {
            if (done.Contains(building.Id))
            {
                return;
            }

            path.Add(building.Id);
            for (var i = 0; i < building.Prerequisites.Length; i++)
            {
                var id = building.Prerequisites[i];
                var start = path.IndexOf(id);
                if (start >= 0)
                {
                    var cycle = path.Skip(start).Append(id);
                    ItemError(building.Id, "prerequisites", i, $"prerequisite cycle: {string.Join(" -> ", cycle)}");
                }
                else if (rules.TryGetBuilding(id, out var prerequisite))
                {
                    Visit(prerequisite, done, path);
                }
            }

            path.RemoveAt(path.Count - 1);
            done.Add(building.Id);
        }

        private void FactionOf(string id, string faction)
        {
            if (!rules.TryGetFaction(faction, out _))
            {
                Error(id, "faction", Missing(id, "faction", faction));
            }
        }

        private void Prerequisites(string id, ImmutableArray<string> prerequisites)
        {
            for (var i = 0; i < prerequisites.Length; i++)
            {
                if (!rules.TryGetBuilding(prerequisites[i], out _))
                {
                    ItemError(id, "prerequisites", i, Missing(id, "building", prerequisites[i]));
                }
            }
        }

        private void UnitOf(string faction, string key, string id)
        {
            if (!rules.TryGetUnit(id, out var unit))
            {
                Error(faction, key, Missing(faction, "unit", id));
            }
            else if (unit.Faction != faction)
            {
                Error(faction, key, $"'{faction}' refers to unit '{id}', but it belongs to '{unit.Faction}'");
            }
        }

        private void BuildingOf(string faction, string key, string id)
        {
            if (!rules.TryGetBuilding(id, out var building))
            {
                Error(faction, key, Missing(faction, "building", id));
            }
            else if (building.Faction != faction)
            {
                Error(faction, key, $"'{faction}' refers to building '{id}', but it belongs to '{building.Faction}'");
            }
        }

        /// <summary>The reason for a reference that does not lead to a definition of the expected kind.</summary>
        private string Missing(string id, string kind, string target)
        {
            var actual = KindOf(target);
            return actual is null
                ? $"'{id}' refers to {kind} '{target}', which is not defined"
                : $"'{id}' refers to {kind} '{target}', but '{target}' is {actual}";
        }

        private string? KindOf(string id) =>
            rules.TryGetUnit(id, out _) ? "unit"
            : rules.TryGetBuilding(id, out _) ? "building"
            : rules.TryGetWeapon(id, out _) ? "weapon"
            : rules.TryGetLocomotor(id, out _) ? "locomotor"
            : rules.TryGetTerrain(id, out _) ? "terrain"
            : rules.TryGetFaction(id, out _) ? "faction"
            : null;

        private void Positive(string id, string key, int value)
        {
            if (value <= 0)
            {
                Error(id, key, $"'{key}' of '{id}' must be above zero");
            }
        }

        private void NotNegative(string id, string key, int value)
        {
            if (value < 0)
            {
                Error(id, key, $"'{key}' of '{id}' must not be negative");
            }
        }

        /// <summary>An error at the value of a field; at the id when the field is not written and its default is wrong.</summary>
        private void Error(string id, string key, string reason) => errors.Add(At(sources, id, key, reason));

        /// <summary>An error at one item of a list field.</summary>
        private void ItemError(string id, string key, int index, string reason)
        {
            var (fileName, node) = Place(id, key);
            if (node is YamlSequenceNode sequence && index < sequence.Children.Count)
            {
                node = sequence.Children[index];
            }

            Add(fileName, node, reason);
        }

        /// <summary>An error at the locomotor key of a speed in a land type.</summary>
        private void SpeedError(string id, string locomotor, string reason)
        {
            var (fileName, node) = Place(id, "speed");
            if (node is YamlMappingNode mapping)
            {
                foreach (var entry in mapping)
                {
                    if (entry.Key is YamlScalarNode { Value: { } key } && key == locomotor)
                    {
                        node = entry.Key;
                        break;
                    }
                }
            }

            Add(fileName, node, reason);
        }

        private (string FileName, YamlNode Node) Place(string id, string key)
        {
            var source = sources[id];
            return source.Fields.TryGetValue(key, out var field) ? (field.FileName, field.Value) : (source.FileName, source.IdNode);
        }

        private void Add(string fileName, YamlNode node, string reason) => errors.Add(RulesFormatException.At(fileName, node, reason).Error);
    }
}
