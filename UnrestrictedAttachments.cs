using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using System.Reflection;
using System.Text;

namespace UnrestrictedAttachments;


public record ModMetadata : AbstractModMetadata {
    public override string ModGuid { get; init; } = "com.mattdokn.unrestrictedattachments";
    public override string Name { get; init; } = "UnrestrictedAttachments";
    public override string Author { get; init; } = "Mattdokn";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; } = "https://github.com/m-barneto/UnrestrictedAttachments";
    public override bool? IsBundleMod { get; init; } = false;
    public override string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 2)]
public class AmmoStats(
    DatabaseServer databaseServer,
    DatabaseService databaseService,
    LocaleService localeService,
    ModHelper modHelper,
    ConfigServer configServer,
    ItemHelper itemHelper,
    ISptLogger<AmmoStats> logger)
    : IOnLoad {

    Dictionary<MongoId, TemplateItem>? itemDatabase;
    Dictionary<string, string>? locales;
    ModConfig? config;

    public Task OnLoad() {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        config = modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "config.json");
        if (config == null) {
            logger.Error("Unable to locate mod config file!");
            return Task.CompletedTask;
        }

        itemDatabase = databaseServer.GetTables().Templates.Items;
        locales = localeService.GetLocaleDb();

        Dictionary<MongoId, List<MongoId>> mods = new();

        // Loop through all items
        foreach (var (itemId, item) in itemDatabase) {
            if (item.Type != "Item") continue;

            TemplateItemProperties? props = item.Properties;
            if (props == null) continue;

            if (!itemHelper.IsOfBaseclass(itemId, BaseClasses.MOD)) continue;

            if (!mods.ContainsKey(item.Parent)) {
                mods.Add(item.Parent, new List<MongoId>([itemId]));
            } else {
                mods[item.Parent].Add(itemId);
            }
        }

        HashSet<string> slotnames = new();
        // Loop through a second time
        foreach (var (itemId, item) in itemDatabase) {
            if (item.Type != "Item" || item.Properties == null) continue;

            // Remove conflicting items
            if (item.Properties.ConflictingItems != null) {
                item.Properties.ConflictingItems.Clear();
            }

            // If item has mod slots
            if (item.Properties.Slots != null) {
                foreach (var slot in item.Properties.Slots) {
                    slotnames.Add(slot.Name);
                }
            }
        }

        foreach (var slotname in slotnames) {
            logger.Info(slotname);
        }


        return Task.CompletedTask;
    }
}

public record ModConfig {
}