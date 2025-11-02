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

namespace WTT_CornerStore
{
    public record ModMetaData : AbstractModMetadata
    {
        public override string ModGuid { get; init; } = "com.libertypiratewtt.cornerstore";
        public override string Name { get; init; } = "WTT - Corner Store";
        public override string Author { get; init; } = "RockaHorse";
        public override SemanticVersioning.Version Version { get; init; } = new("1.0.0");
        public override Range SptVersion { get; init; } = new("~4.0.2");
        public override string License { get; init; } = "MIT";
        public override bool? IsBundleMod { get; init; } = true;
        public override List<string>? Contributors { get; init; } = null;
        public override List<string>? Incompatibilities { get; init; } = null;
        public override Dictionary<string, Range>? ModDependencies { get; init; } = null;
        public override string? Url { get; init; } = null;
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
            await wttCommon.CustomBuffService.CreateCustomBuffs(assembly, Path.Join("db", "WTTBuffs"));
            await wttCommon.CustomLootspawnService.CreateCustomLootSpawns(assembly);
            await wttCommon.CustomStaticSpawnService.CreateCustomStaticSpawns(assembly);

            logger.Success("Shelves stocked and spirits await.");
        }
    }
}