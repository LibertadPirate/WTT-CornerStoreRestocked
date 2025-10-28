using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using SPTarkov.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using WTTServerCommonLib;
using Range = SemanticVersioning.Range;
using System.Collections.Immutable;

public class Buff
{
    public string BuffType { get; set; }
    public double Chance { get; set; }
    public double Delay { get; set; }
    public double Duration { get; set; }
    public double Value { get; set; }
    public bool AbsoluteValue { get; set; }
    public string SkillName { get; set; }
}

public class CustomBuffsRoot
{
    public Dictionary<string, List<Buff>> Buffs { get; set; }
}

namespace WTT_CornerStore
{
    public record ModMetaData : AbstractModMetadata
    {
        public override string ModGuid { get; init; } = "com.WTT.CornaStow";
        public override string Name { get; init; } = "WTT - Corner Store";
        public override string Author { get; init; } = "RockaHorse";
        public override SemanticVersioning.Version Version { get; init; } = new("1.0.0");
        public override Range SptVersion { get; init; } = new("4.0.2");
        public override string License { get; init; } = "MIT";
        public override bool? IsBundleMod { get; init; } = true;
        public override List<string>? Contributors { get; init; } = null;
        public override List<string>? Incompatibilities { get; init; } = null;
        public override Dictionary<string, Range>? ModDependencies { get; init; } = null;
        public override string? Url { get; init; } = null;
    }

    // Inject Buffs before Using WTTCustomLib
    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader)]
    public class WttBuffInjector(
        DatabaseService databaseService,
        ISptLogger<WttBuffInjector> logger
    ) : IOnLoad
    {
        private static readonly ImmutableHashSet<string> ValidBuffTypes = new HashSet<string>
        {
            "HealthRate", "EnergyRate", "HydrationRate", "SkillRate", "MaxStamina", "StaminaRate",
            "StomachBloodloss", "ContusionBlur", "ContusionWiggle", "Pain", "HandsTremor",
            "QuantumTunnelling", "RemoveNegativeEffects", "RemoveAllBuffs", "RemoveAllBloodLosses",
            "DamageModifier", "WeightLimit", "UnknownToxin", "LethalToxin", "Antidote",
            "BodyTemperature", "LightBleeding", "HeavyBleeding", "Fracture", "Contusion",
            "HalloweenBuff", "MisfireEffect", "ZombieInfection", "FrostbiteBuff"
        }.ToImmutableHashSet();

        public async Task OnLoad()
        {
            InjectCustomBuffs();
        }

        private void InjectCustomBuffs()
        {
            try
            {
                string modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string jsonPath = Path.Combine(modDir, "db", "WTTBuffs", "customBuffs.json");

                logger.Info($"Loading buffs from: {jsonPath}");

                if (!File.Exists(jsonPath))
                {
                    logger.Error($"customBuffs.json not found at: {jsonPath}");
                    return;
                }

                string jsonContent = File.ReadAllText(jsonPath);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var customBuffs = JsonSerializer.Deserialize<CustomBuffsRoot>(jsonContent, options);

                if (customBuffs?.Buffs == null || customBuffs.Buffs.Count == 0)
                {
                    logger.Error("No buffs deserialized—check JSON format.");
                    return;
                }

                logger.Info($"Deserialized {customBuffs.Buffs.Count} custom buff keys.");

                foreach (var kvp in customBuffs.Buffs)
                {
                    logger.Info($"Buff key: {kvp.Key}, entries: {kvp.Value.Count}");

                    var sptBuffList = new List<SPTarkov.Server.Core.Models.Eft.Common.Buff>();

                    foreach (var customBuff in kvp.Value)
                    {
                        if (!ValidBuffTypes.Contains(customBuff.BuffType))
                        {
                            logger.Warning($"Invalid BuffType '{customBuff.BuffType}' in {kvp.Key}—skipping.");
                            continue;
                        }

                        if (customBuff.BuffType == "SkillRate" && string.IsNullOrEmpty(customBuff.SkillName))
                        {
                            logger.Warning($"SkillRate buff in {kvp.Key} missing SkillName—skipping.");
                            continue;
                        }

                        sptBuffList.Add(new SPTarkov.Server.Core.Models.Eft.Common.Buff
                        {
                            BuffType = customBuff.BuffType,
                            Chance = customBuff.Chance,
                            Delay = customBuff.Delay,
                            Duration = customBuff.Duration,
                            Value = customBuff.Value,
                            AbsoluteValue = customBuff.AbsoluteValue,
                            SkillName = customBuff.SkillName
                        });
                    }

                    var globals = databaseService.GetGlobals();
                    var stimulatorBuffs = globals.Configuration.Health.Effects.Stimulator.Buffs;

                    if (stimulatorBuffs.ContainsKey(kvp.Key))
                    {
                        logger.Warning($"Overwriting existing buff: {kvp.Key}");
                    }

                    stimulatorBuffs[kvp.Key] = sptBuffList;
                    logger.Info($"Added buff: {kvp.Key}");
                }

                logger.Info($"Injected {customBuffs.Buffs.Count} custom buffs into globals!");
            }
            catch (Exception ex)
            {
                logger.Error($"Error injecting buffs: {ex.Message}");
            }
        }
    }


    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 2)]
    public class WttItemCreator(
        WTTServerCommonLib.WTTServerCommonLib wttCommon,
        ISptLogger<WttItemCreator> logger
    ) : IOnLoad
    {
        public async Task OnLoad()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            await wttCommon.CustomItemServiceExtended.CreateCustomItems(assembly, Path.Join("db", "Items", "Consumables"));
            await wttCommon.CustomLootspawnService.CreateCustomLootSpawns(assembly);
            await wttCommon.CustomStaticSpawnService.CreateCustomStaticSpawns(assembly);

            logger.Success("Shelves stocked and spirits await.");
        }
    }
}