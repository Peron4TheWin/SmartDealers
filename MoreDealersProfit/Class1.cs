using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI.Phone.Messages;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Contract = Il2CppScheduleOne.Quests.Contract;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(MoreDealersProfit.EntryPoint), "SmartDealers", "1.0.0", "_peron")]
namespace MoreDealersProfit;

public class EntryPoint : MelonMod
{
    public static MelonPreferences_Category Category;
    public static MelonPreferences_Entry<bool> MoreCustomers;
    public static MelonPreferences_Entry<bool> MoreProfit;
    public static MelonPreferences_Entry<float> SpeedMultipliey;
    public static MelonPreferences_Entry<int> SigningFee;
    public static MelonPreferences_Entry<float> Cut;
    public static MelonPreferences_Entry<bool> AutoPickupMoney;
    public override void OnInitializeMelon()
    {
        Category = MelonPreferences.CreateCategory("EnhancedDealers_settings", "More Dealers Profit settings");
        MoreCustomers = Category.CreateEntry("moreCustomers", false, "Allows more customers to be assigned to dealers");
        MoreProfit = Category.CreateEntry("moreProfit", false, "Allows more profit from dealers (Something like a counter offer)");
        SpeedMultipliey = Category.CreateEntry("SpeedMultiplier", 1.0f, "Allows more speed for dealers");
        SigningFee = Category.CreateEntry("SigningFee", 500, "Modifies signing fee for dealers");
        Cut = Category.CreateEntry("Cut", 0.2f, "Modifies cut for dealers.");
        AutoPickupMoney = Category.CreateEntry("AutoMoneyPickup", false, "Automatically collects money from dealers");
        Category.SetFilePath("UserData/EnhancedDealers.cfg",true,false);
        Category.SaveToFile();
    }

    public static float EvaluateCounterofferPercentage(ProductDefinition product, int quantity, float price, Customer customer,string productid)
    {
        float adjustedWeeklySpend = customer.customerData.GetAdjustedWeeklySpend(customer.NPC.RelationData.RelationDelta / 5f);
        Il2CppSystem.Collections.Generic.List<EDay> orderDays = customer.customerData.GetOrderDays(customer.CurrentAddiction, customer.NPC.RelationData.RelationDelta / 5f);
        float num = adjustedWeeklySpend / orderDays.Count;
        // Immediate rejection based on price threshold
        if (price >= num * 3f) return 0f;
        float valueProposition = Customer.GetValueProposition(Registry.GetItem<ProductDefinition>(productid), price / quantity);
        float productEnjoyment = customer.GetProductEnjoyment(product, customer.customerData.Standards.GetCorrespondingQuality());
        float num2 = Mathf.InverseLerp(-1f, 1f, productEnjoyment);
        float valueProposition2 = Customer.GetValueProposition(product, price / quantity);
        float num3 = Mathf.Pow(quantity / (float)quantity, 0.6f);
        float num4 = Mathf.Lerp(0f, 2f, num3 * 0.5f);
        float num5 = Mathf.Lerp(1f, 0f, Mathf.Abs(num4 - 1f));
        // High value proposition leads to acceptance
        if (valueProposition2 * num5 > valueProposition) return 100f;
        // Low value proposition leads to rejection
        if (valueProposition2 < 0.12f) return 0f;
        float num6 = productEnjoyment * valueProposition;
        float num7 = num2 * num5 * valueProposition2;
        // Better product enjoyment and proposition leads to acceptance
        if (num7 > num6) return 100f;
        float num8 = num6 - num7;
        float num9 = Mathf.Lerp(0f, 1f, num8 / 0.2f);
        float t = Mathf.Max(customer.CurrentAddiction, customer.NPC.RelationData.NormalizedRelationDelta);
        float num10 = Mathf.Lerp(0f, 0.2f, t);
        // Calculate probabilistic acceptance chance
        if (num9 <= num10) return 100f;
        if (num9 - num10 >= 0.9f) return 0f;
        float probability = (0.9f + num10 - num9) / 0.9f;
        return Mathf.Clamp(probability, 0f, 1f) * 100f;
    }
    


    [HarmonyPatch(typeof(Il2CppScheduleOne.UI.Phone.Messages.DealerManagementApp))]
    class DealerApp
    {
        [HarmonyPatch(nameof(Il2CppScheduleOne.UI.Phone.Messages.DealerManagementApp.SetDisplayedDealer))]
        [HarmonyPostfix]
        public static void Postfix(Il2CppScheduleOne.UI.Phone.Messages.DealerManagementApp __instance, Il2CppScheduleOne.Economy.Dealer dealer)
        {
            if ((bool)MoreCustomers.BoxedValue)
            {
                __instance.CustomerTitleLabel.text = "Assigned Customers: "+ dealer.AssignedCustomers.Count.ToString();
                __instance.AssignCustomerButton.gameObject.SetActive(true);
            }
        }
        
        [HarmonyPatch(nameof(Il2CppScheduleOne.UI.Phone.Messages.DealerManagementApp.Awake))]
        [HarmonyPostfix]
        public static void Awake(Il2CppScheduleOne.UI.Phone.Messages.DealerManagementApp __instance)
        {
            var dealerApp = __instance;
            var existingEntries = dealerApp.CustomerEntries;
            var entryList = new List<RectTransform>(existingEntries);
            var contentTransform = __instance.gameObject.transform.Find("Container/Background/Content");
            var customersTransform = contentTransform?.Find("Customers");
            if (customersTransform == null) return;
            int unlockedCustomerCount=0;
            foreach (var npc in NPCManager.NPCRegistry)
            {
                var customer = npc.gameObject.GetComponent<Customer>();
                if (customer != null)
                {
                    unlockedCustomerCount++;
                    var lastEntry = existingEntries[^1]; // last element
                    var newEntry = Object.Instantiate(lastEntry, lastEntry.parent);
                    newEntry.name = $"CustomerEntry ({entryList.Count})";
                    entryList.Add(newEntry);
                }
            }
            dealerApp.CustomerEntries = new Il2CppReferenceArray<RectTransform>(entryList.ToArray());
            var backgroundTransform = __instance.gameObject.transform.Find("Container/Background");
            var bgRect = backgroundTransform.GetComponent<RectTransform>();
            var scrollContent = new GameObject("ScrollingContent").AddComponent<RectTransform>();
            scrollContent.SetParent(backgroundTransform, false);
            scrollContent.anchorMin = new Vector2(0f, 1f);
            scrollContent.anchorMax = new Vector2(1f, 1f);
            scrollContent.pivot = new Vector2(0.5f, 1f);
            scrollContent.anchoredPosition = Vector2.zero;
            scrollContent.sizeDelta = new Vector2(0f, bgRect.rect.height + unlockedCustomerCount*100f);
            var title = backgroundTransform.Find("Title");
            var content = backgroundTransform.Find("Content");
            if (title != null) title.SetParent(scrollContent, true);
            if (content != null) content.SetParent(scrollContent, true);
            if (!backgroundTransform.TryGetComponent(out Mask mask)) backgroundTransform.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var scrollRect = backgroundTransform.GetComponent<ScrollRect>() ?? backgroundTransform.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = bgRect;
            scrollRect.content = scrollContent;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.elasticity = 0f;
            scrollRect.scrollSensitivity = 30f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
            scrollRect.verticalNormalizedPosition = 1f;
            var assign = customersTransform.Find("Assign");
            if (assign != null)
            {
                assign.SetSiblingIndex(0);
                assign.gameObject.SetActive(true);
            }
        }
    }
    
    [HarmonyPatch(typeof(Il2CppScheduleOne.Economy.Dealer))]
    class Dealer
    {
        [HarmonyPatch(nameof(Il2CppScheduleOne.Economy.Dealer.Awake))]
        [HarmonyPostfix]
        public static void Postfix(Il2CppScheduleOne.Economy.Dealer __instance)
        {
            if (__instance == null) return;
            __instance.SigningFee=(int)SigningFee.BoxedValue;
            __instance.Cut=(float)Cut.BoxedValue;
            NPCSpeedController tmp = __instance.GetComponent<NPCSpeedController>();
            tmp.SpeedMultiplier = (float)SpeedMultipliey.BoxedValue;
        }
        
        [HarmonyPatch(nameof(Il2CppScheduleOne.Economy.Dealer.CheckAttendStart))]
        [HarmonyPostfix]
        public static void Postfixxx(Il2CppScheduleOne.Economy.Dealer __instance)
        {
            if (__instance == null) return;
            if ((bool)AutoPickupMoney.BoxedValue)
            {
                __instance.CollectCash();
            }
        }
        [HarmonyPatch(nameof(Il2CppScheduleOne.Economy.Dealer.CustomerContractStarted))]
        [HarmonyPostfix]
        public static void Postfx(Il2CppScheduleOne.Economy.Dealer __instance)
        {
            if (__instance == null) return;
            if ((bool)AutoPickupMoney.BoxedValue)
            {
                __instance.CollectCash();
            }
        }
        
        [HarmonyPatch(nameof(Il2CppScheduleOne.Economy.Dealer.CompletedDeal))]
        [HarmonyPostfix]
        public static void Postfixx(Il2CppScheduleOne.Economy.Dealer __instance)
        {
            if (__instance == null) return;
            if ((bool)AutoPickupMoney.BoxedValue)
            {
                __instance.CollectCash();
            }
        }
        
        [HarmonyPatch(nameof(Il2CppScheduleOne.Economy.Dealer.CustomerContractStarted))]
        [HarmonyPostfix]
        public static void Postfix(Il2CppScheduleOne.Economy.Dealer __instance, Contract contract)
        {
            if (__instance == null) return;
            if (__instance.ActiveContracts.Count<=0) return;
            //if (!changed) SetupScrollArea();
            if (!(bool)MoreProfit.BoxedValue) return;
            var a = contract.ProductList.entries[0];
            ProductDefinition product = Registry.GetItem<ProductDefinition>(a.ProductID);
           if (product == null || contract.Customer == null || contract.Customer.GetComponent<Customer>() == null)
            {
                MelonLogger.Error("Null reference encountered while evaluating counteroffer percentage.");
                return;
            }
            
            float chance = EntryPoint.EvaluateCounterofferPercentage(
                product,
                a.Quantity,
                contract.Payment,
                contract.Customer.GetComponent<Customer>(),
                a.ProductID
            );
            while (Mathf.RoundToInt(chance) == 100)
            {
                contract.Payment += 1;
                chance = EntryPoint.EvaluateCounterofferPercentage(
                    product,
                    a.Quantity,
                    contract.Payment,
                    contract.Customer.GetComponent<Customer>(),
                    a.ProductID
                );
            }
        }
    }
}