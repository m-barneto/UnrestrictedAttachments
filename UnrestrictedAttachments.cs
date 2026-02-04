using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;

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
    LocaleService localeService,
    ModHelper modHelper,
    ItemHelper itemHelper,
    ISptLogger<AmmoStats> logger)
    : IOnLoad {

    Dictionary<MongoId, TemplateItem>? itemDatabase;
    Dictionary<string, string>? locales;
    ModConfig? config;

    Dictionary<string, MongoId> slotToMod = new() {
        {"mod_pistol_grip", BaseClasses.PISTOL_GRIP },
        {"mod_magazine", BaseClasses.MAGAZINE },
        {"mod_receiver", BaseClasses.RECEIVER },
        {"mod_stock", BaseClasses.STOCK },
        {"mod_charge", BaseClasses.CHARGE },
        {"mod_barrel", BaseClasses.BARREL },
        {"mod_handguard", BaseClasses.HANDGUARD },
        {"mod_mount", BaseClasses.MOUNT },
        {"mod_muzzle", BaseClasses.MUZZLE },
        {"mod_scope", BaseClasses.OPTIC_SCOPE },// come back to this
        {"mod_sight_rear", BaseClasses.SIGHTS }, // rear sight?
        {"mod_tactical", BaseClasses.TACTICAL_COMBO },
        {"mod_gas_block", BaseClasses.GASBLOCK },
        {"mod_launcher", BaseClasses.LAUNCHER },
        {"mod_foregrip", BaseClasses.FOREGRIP },
        {"mod_sight_front", BaseClasses.SIGHTS }, // front sight?
        {"mod_equipment", BaseClasses.EQUIPMENT },
        {"mod_flashlight", BaseClasses.FLASHLIGHT },
        {"mod_bipod", BaseClasses.BIPOD },
        {"mod_pistol_grip_akms", BaseClasses.PISTOL_GRIP }, //akms special
        {"mod_stock_akms", BaseClasses.STOCK }, // akms special
        {"mod_nvg", BaseClasses.SPECIAL_SCOPE }, // maybe special scope?
        {"mod_stock_axis", BaseClasses.STOCK }, // axis special
        {"mod_trigger", BaseClasses.MOD }, // IDK
        {"mod_hammer", BaseClasses.MOD }, //IDK
        {"mod_catch", BaseClasses.MOD } // IDK
    };

    public Task OnLoad() {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        config = modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "config.json");
        if (config == null) {
            logger.Error("Unable to locate mod config file!");
            return Task.CompletedTask;
        }

        itemDatabase = databaseServer.GetTables().Templates.Items;
        locales = localeService.GetLocaleDb();

        Dictionary<string, HashSet<MongoId>> slotToMods = new();

        foreach (var (itemId, item) in itemDatabase) {
            if (item.Properties == null || item.Properties.Slots == null) continue;
            foreach (var slot in item.Properties.Slots) {
                if (slot.Name == null) continue;
                string slotName = NormalizeModSlot(slot.Name);
                if (!slotName.Contains("mod")) continue;

                if (!slotToMods.ContainsKey(slotName)) slotToMods.Add(slotName, new HashSet<MongoId>());

                if (slot.Properties == null || slot.Properties.Filters == null || slot.Properties.Filters.Count() == 0) continue;

                foreach(var filter in slot.Properties.Filters) {
                    if (filter.Filter == null) continue;
                    slotToMods[slotName].UnionWith(filter.Filter);
                }
            }
        }

        foreach (var (itemId, item) in itemDatabase) {
            if (item.Properties == null || item.Properties.Slots == null) continue;
            foreach (var slot in item.Properties.Slots) {
                if (slot.Name == null) continue;
                string slotName = NormalizeModSlot(slot.Name);
                

                if (!slotToMods.ContainsKey(slotName)) continue;

                if (slot.Properties == null || slot.Properties.Filters == null || slot.Properties.Filters.Count() == 0) continue;

                slot.Properties.Filters.First().Filter = slotToMods[slotName];
            }
        }

        foreach (var (modSlot, mods) in slotToMods) {
            logger.Success(modSlot);
            foreach (var mod in mods) {
                if (locales.ContainsKey($"{mod} Name")){
                    logger.Info($"    {locales[$"{mod} Name"]}");
                } else {
                    logger.Warning(mod);
                }
            }
        }

        return Task.CompletedTask;
/*
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

            if (itemHelper.IsOfBaseclass(itemId, BaseClasses.ASSAULT_SCOPE)) {
                logger.Info($"{locales[itemId + " Name"]} assault");
            }
            if (itemHelper.IsOfBaseclass(itemId, BaseClasses.OPTIC_SCOPE)) {
                logger.Info($"{locales[itemId + " Name"]} optic");
            }
            if (itemHelper.IsOfBaseclass(itemId, BaseClasses.SPECIAL_SCOPE)) {
                logger.Info($"{locales[itemId + " Name"]} special");
            }
        }

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
                    if (!slot.Name.Contains("mod")) continue;
                    string slotname = NormalizeModSlot(slot.Name);
                }
            }
        }


        return Task.CompletedTask;*/
    }

    static string NormalizeModSlot(string input) {
        input = input.ToLowerInvariant();

        input = input.Replace("pistolgrip", "pistol_grip");
        input = input.Replace("reciever", "receiver");

        input = Regex.Replace(input, @"_?\d+$", "");

        if (input.StartsWith("mod_tactical"))
            return "mod_tactical";

        if (input.StartsWith("mod_mount"))
            return "mod_mount";

        if (input.StartsWith("mod_scope"))
            return "mod_scope";

        if (input.StartsWith("mod_equipment"))
            return "mod_equipment";

        if (input.StartsWith("mod_muzzle"))
            return "mod_muzzle";

        if (input.StartsWith("mod_stock") && !input.Contains("axis") && !input.Contains("akms"))
            return "mod_stock";

        return input;
    }

    static MongoId? SlotToMods(string slotname) {
        string slot = NormalizeModSlot(slotname);



        return null;
    }
}

public record ModConfig {
}