﻿using System;
using System.Reflection;
using Harmony;
using Studio;
using UILib;
using UnityEngine;
using UnityEngine.UI;
using Illusion.Game;
using MessagePack;
using Logger = BepInEx.Logger;
using KKAPI.Chara;
using System.Linq;
using System.Collections.Generic;
using KoiClothesOverlayX;
using System.Collections;

namespace KK_CostumeInfo_Patches
{
	internal class CostumeInfo_Patches
	{
		internal static void InitPatch(HarmonyInstance harmony)
		{
            harmony.Patch(typeof(MPCharCtrl).GetMethod("OnClickRoot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy), null, new HarmonyMethod(typeof(CostumeInfo_Patches), "OnClickRootPostfix", null), null);
            harmony.Patch(typeof(MPCharCtrl).GetNestedType("CostumeInfo", BindingFlags.NonPublic).GetMethod("Init", BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy), null, new HarmonyMethod(typeof(CostumeInfo_Patches), "InitPostfix", null), null);
            harmony.Patch(typeof(MPCharCtrl).GetNestedType("CostumeInfo", BindingFlags.NonPublic).GetMethod("OnClickLoad", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy), new HarmonyMethod(typeof(CostumeInfo_Patches), "OnClickLoadPrefix", null), new HarmonyMethod(typeof(CostumeInfo_Patches), "OnClickLoadPostfix", null), null);
            Logger.Log(BepInEx.Logging.LogLevel.Debug,"[KK_SCLO] Patch Insert Complete");
		}
        public static CharaFileSort charaFileSort;
		private static void InitPostfix(object __instance)
		{
            Logger.Log(BepInEx.Logging.LogLevel.Debug,"[KK_SCLO] Init Patch");
			charaFileSort = (CharaFileSort)__instance.GetPrivate("fileSort");
			Transform parent = charaFileSort.root.parent;

            Type ClothesKind = typeof(ChaFileDefine.ClothesKind);
            Array ClothesKindArray = Enum.GetValues(ClothesKind);

            Image panel = UILib.UIUtility.CreatePanel("TooglePanel", parent.parent.parent);
            Button btnAll = UILib.UIUtility.CreateButton("BtnAll", panel.transform, "all");
            btnAll.GetComponentInChildren<Text>(true).color = Color.white;
            btnAll.GetComponent<Image>().color = Color.gray;
            btnAll.transform.SetRect(Vector2.zero, new Vector2(1f, 0f), new Vector2(5f, 25f+20f*ClothesKindArray.Length), new Vector2(-5f, 50f+20f*(ClothesKindArray.Length)));
            btnAll.GetComponentInChildren<Text>(true).transform.SetRect(Vector2.zero, new Vector2(1f, 1f), new Vector2(5f, 1f), new Vector2(-5f, -2f));

            Toggle[] tgls = new Toggle[ClothesKindArray.Length+1];
            for (int i = 0; i < ClothesKindArray.Length; i++) { 
                tgls[i] = UILib.UIUtility.CreateToggle(ClothesKindArray.GetValue(i).ToString(), panel.transform, ClothesKindArray.GetValue(i).ToString());
                tgls[i].GetComponentInChildren<Text>(true).alignment = TextAnchor.UpperLeft;
                tgls[i].GetComponentInChildren<Text>(true).color = Color.white;
                tgls[i].transform.SetRect(Vector2.zero, new Vector2(1f, 0f), new Vector2(5f, 20f*(ClothesKindArray.Length-i)), new Vector2(5f, 25f+20f*(ClothesKindArray.Length-i)));
                tgls[i].GetComponentInChildren<Text>(true).transform.SetRect(Vector2.zero, new Vector2(1f, 1f), new Vector2(20f, 1f), new Vector2(-5f, -2f));
            }

            tgls[ClothesKindArray.Length] = UILib.UIUtility.CreateToggle("ToggleAccessories", panel.transform, "accessories");
            tgls[ClothesKindArray.Length].GetComponentInChildren<Text>(true).alignment = TextAnchor.UpperLeft;
            tgls[ClothesKindArray.Length].GetComponentInChildren<Text>(true).color = Color.white;
            tgls[ClothesKindArray.Length].transform.SetRect(Vector2.zero, new Vector2(1f, 0f), new Vector2(5f, 0f), new Vector2(5f, 25f));
            tgls[ClothesKindArray.Length].GetComponentInChildren<Text>(true).transform.SetRect(Vector2.zero, new Vector2(1f, 1f), new Vector2(20f, 1f), new Vector2(-5f, -2f));

			panel.transform.SetRect(Vector2.zero, new Vector2(1f, 0f), new Vector2(407f, 285f-ClothesKindArray.Length*20), new Vector2(150f, 340f));
            panel.GetComponent<Image>().color = new Color32(80,80,80,220);

            btnAll.onClick.RemoveAllListeners();
            btnAll.onClick.AddListener(delegate ()
            {
                bool flag = false;
                for (int i = 0; i < tgls.Length; i++)
                {
                    if (!tgls[i].isOn)
                    {
                        flag = true;
                    }
                    tgls[i].isOn = true;
                }
                if (!flag)
                {
                    for (int j = 0; j < tgls.Length; j++)
                    {
                        tgls[j].isOn = false;
                    }
                }
            });
        }

        private static MPCharCtrl mpCharCtrl;
        private static KoiClothesOverlayController KCOXController;
        public static void OnClickRootPostfix(MPCharCtrl __instance, int _idx)
        {
            if (_idx > 0 && __instance != null)
            {
                mpCharCtrl = __instance;
                Logger.Log(BepInEx.Logging.LogLevel.Debug,"[KK_SCLO] Get mpCharCtrl");
            }
        }

        static int[] clothesIdBackup = null;
        static ChaFileClothes.PartsInfo.ColorInfo[][] clothesColorBackup = null;
        static int[] subClothesIdBackup = null;
        static ChaFileAccessory.PartsInfo[] accessoriesPartsBackup = null;
        static Dictionary<string,ClothesTexData> KCOXTexDataBackup = null;
        public static void OnClickLoadPrefix()
        {
            Logger.Log(BepInEx.Logging.LogLevel.Debug,"[KK_SCLO] Onclick Patch Start");
			ChaControl chaCtrl = mpCharCtrl.ociChar.charInfo;
            if (chaCtrl == null)
            {
                Logger.Log(BepInEx.Logging.LogLevel.Error,"[KK_SCLO] Get chaCtrl FAILED");
                return;
            }
            Logger.Log(BepInEx.Logging.LogLevel.Debug,"[KK_SCLO] Get ChaCtrl");
			ChaFileClothes clothes = chaCtrl.nowCoordinate.clothes;
			ChaFileAccessory accessories = chaCtrl.nowCoordinate.accessory;

            //Backup
            clothesIdBackup = new int[clothes.parts.Length];
            clothesColorBackup = new ChaFileClothes.PartsInfo.ColorInfo[clothes.parts.Length][];
            subClothesIdBackup = new int[clothes.subPartsId.Length];
            accessoriesPartsBackup = new ChaFileAccessory.PartsInfo[accessories.parts.Length];
			for (int i = 0; i < clothes.parts.Length; i++)
			{
				clothesIdBackup[i] = clothes.parts[i].id;
                clothesColorBackup[i] = clothes.parts[i].colorInfo;
                Logger.Log(BepInEx.Logging.LogLevel.Debug, "[KK_SCLO] Get original: " + MainClothesNames[i] + "/ ID: "+clothes.parts[i].id);
			}
			for (int j = 0; j < clothes.subPartsId.Length; j++)
			{
				subClothesIdBackup[j] = clothes.subPartsId[j];
                Logger.Log(BepInEx.Logging.LogLevel.Debug, "[KK_SCLO] Get original: " + SubClothesNames[j] + "/ ID: "+clothes.subPartsId[j]);
			}
            for (int i = 0; i < accessories.parts.Length; i++)
            {
                accessoriesPartsBackup[i] = accessories.parts[i];
                Logger.Log(BepInEx.Logging.LogLevel.Debug, "[KK_SCLO] Get original Accessory: "+accessories.parts[i].id);
            }
            Logger.Log(BepInEx.Logging.LogLevel.Debug,"[KK_SCLO] Get original coordinate SUCCESS");

            //KCOXBackup
            KCOXController = CharacterApi.GetBehaviours(chaCtrl).OfType<KoiClothesOverlayController>().First();
            if (null == KCOXController)
            {
                Logger.Log(BepInEx.Logging.LogLevel.Debug,"[KK_SCLO] No KCOX Controller found");
            }
            else
            {
                Logger.Log(BepInEx.Logging.LogLevel.Debug,"[KK_SCLO] KCOX Controller found");
                KCOXTexDataBackup = new Dictionary<string, ClothesTexData>();
                for (int i = 0; i < clothes.parts.Length; i++)
                {
                    getOverlay(MainClothesNames[i], chaCtrl.objClothes[i]?.GetComponent<ChaClothesComponent>());
                }
                for (int j = 0; j < clothes.subPartsId.Length; j++)
                {
                    getOverlay(SubClothesNames[j], chaCtrl.objParts[j]?.GetComponent<ChaClothesComponent>());
                }
                Logger.Log(BepInEx.Logging.LogLevel.Debug, "[KK_SCLO] Get original Overlay SUCCESS");
            }
            return;

            bool getOverlay(string name, ChaClothesComponent clothComp)
            {
                if (null == KCOXController.GetOverlayTex(name))
                {
                    KCOXTexDataBackup[name] = null;
                    Logger.Log(BepInEx.Logging.LogLevel.Debug, "[KK_SCLO] " + name + " not found");
                    return false;
                }
                else
                {
                    KCOXTexDataBackup[name] = KCOXController.GetOverlayTex(name);
                    Logger.Log(BepInEx.Logging.LogLevel.Debug, "[KK_SCLO] Get original overlay: " + name);
                    return true;
                }
            }

            //Get whole clothes and whole accessories
            //byte[] bytes = MessagePackSerializer.Serialize<ChaFileClothes>(chaCtrl.nowCoordinate.clothes);
            //byte[] originalAccBytes = MessagePackSerializer.Serialize<ChaFileAccessory>(chaCtrl.nowCoordinate.accessory);
            //Change clothes part
            //clothesIdBackup[kind] = MessagePackSerializer.Serialize<ChaFileClothes.PartsInfo>(clothes.parts[kind]);
            //chaCtrl.nowCoordinate.clothes.parts[kind] = MessagePackSerializer.Deserialize<ChaFileAccessory.PartsInfo>(clothesIdBackup[kind]);

            //Load function
            //chaCtrl.nowCoordinate.LoadFile(fullPath);
            //Logger.Log(BepInEx.Logging.LogLevel.Debug,"[KK_SCLO] Loaded new clothes SUCCESS");
        }

        public static void OnClickLoadPostfix()
        {
            if (null == accessoriesPartsBackup || null == clothesIdBackup || null == subClothesIdBackup)
            {
                Logger.Log(BepInEx.Logging.LogLevel.Error, "[KK_SCLO] Get Backup FAILED");
                cleanBackup();
                return;
            }

            if (null == charaFileSort)
            {
                Logger.Log(BepInEx.Logging.LogLevel.Error, "[KK_SCLO] Get charaFileSort FAILED in postfix");
                cleanBackup();
                return;
            }
            CharaFileSort fileSort = charaFileSort;
            if (fileSort == null || fileSort.select < 0)
            {
                Logger.Log(BepInEx.Logging.LogLevel.Error, "[KK_SCLO] Get filesort ERROR");
                cleanBackup();
                return;
            }
            Toggle[] toggleList = fileSort.root.parent.parent.parent.GetComponentsInChildren<Toggle>();
            if (toggleList.Length < 0)
            {
                Logger.Log(BepInEx.Logging.LogLevel.Error, "[KK_SCLO] Getting ToggleList FAILED");
                cleanBackup();
                return;
            }
            bool flag = true;
            foreach (Toggle tgl in toggleList)
            {
                flag &= tgl.isOn;
            }
            if (flag)
            {
                Logger.Log(BepInEx.Logging.LogLevel.Info, "[KK_SCLO] Toggle all true, skip roll back");
                toggleList = null;
                cleanBackup();
                return;
            }

            ChaControl chaCtrl = mpCharCtrl.ociChar.charInfo;
            if (chaCtrl == null)
            {
                Logger.Log(BepInEx.Logging.LogLevel.Error, "[KK_SCLO] Get chaCtrl FAILED");
                cleanBackup();
                return;
            }
            Logger.Log(BepInEx.Logging.LogLevel.Debug, "[KK_SCLO] Starting to roll back origin clothes");

            foreach (Toggle tgl in toggleList)
            {
                if (!tgl.isOn)
                {
                    /*
                    ChaFileDefine.ClothesKind
                    public enum ClothesKind
                    {
                        top,
                        bot,
                        bra,
                        shorts,
                        gloves,
                        panst,
                        socks,
                        shoes_inner,
                        shoes_outer
                    }
                    */

                    //Change accessories
                    if (String.Equals(tgl.GetComponentInChildren<Text>(true).text, "accessories"))
                    {
                        Logger.Log(BepInEx.Logging.LogLevel.Debug, "[KK_SCLO] ->Roll back:" + tgl.GetComponentInChildren<Text>(true).text);
                        chaCtrl.nowCoordinate.accessory = new ChaFileAccessory();
                        for (int i = 0; i < accessoriesPartsBackup.Length; i++)
                        {
                            chaCtrl.nowCoordinate.accessory.parts[i] = accessoriesPartsBackup[i];
                            chaCtrl.ChangeAccessory(i, accessoriesPartsBackup[i].type, accessoriesPartsBackup[i].id, accessoriesPartsBackup[i].parentKey, true);
                        }
                        continue;
                    }

                    //Discard unknown toggle
                    object tmpToggleType = null;
                    try
                    {
                        tmpToggleType = Enum.Parse(typeof(ChaFileDefine.ClothesKind), tgl.GetComponentInChildren<Text>(true).text);
                    }
                    catch (NullReferenceException)
                    {
                        Logger.Log(BepInEx.Logging.LogLevel.Debug, "[KK_SCLO] Discard Unknown Toggle:" + tgl.GetComponentInChildren<Text>(true).text);
                        continue;
                    }

                    int kind = Convert.ToInt32(tmpToggleType);
                    //Roll back clothes
                    chaCtrl.ChangeClothes(kind, clothesIdBackup[kind], subClothesIdBackup[0], subClothesIdBackup[1], subClothesIdBackup[2], true);
                    chaCtrl.AssignCoordinate((ChaFileDefine.CoordinateType)chaCtrl.fileStatus.coordinateType);
                    rollbackOverlay(true, kind);
                    switch (tmpToggleType)
                    {
                        case ChaFileDefine.ClothesKind.top:
                            for (int j = 0; j < 3; j++)
                            {
                                rollbackOverlay(false, j);
                            }
                            break;
                        case ChaFileDefine.ClothesKind.bot:
                            //TODO: Manage ABMX Skirt
                            break;
                        default:
                            break;
                    }
                    chaCtrl.ChangeClothes(kind, clothesIdBackup[kind], subClothesIdBackup[0], subClothesIdBackup[1], subClothesIdBackup[2], true);
                    chaCtrl.nowCoordinate.clothes.parts[kind].colorInfo = clothesColorBackup[kind];
                    Logger.Log(BepInEx.Logging.LogLevel.Debug, "[KK_SCLO] ->Roll back:" + tgl.GetComponentInChildren<Text>(true).text + " / ID: " + clothesIdBackup[kind]);
                }
            }
            chaCtrl.Reload(false, true, true, true);

            cleanBackup();
            return;

            void cleanBackup()
            {
                clothesIdBackup = null;
                accessoriesPartsBackup = null;
                subClothesIdBackup = null;
                KCOXTexDataBackup = null;
                Logger.Log(BepInEx.Logging.LogLevel.Debug, "[KK_SCLO] Finish");
                Utils.Sound.Play(SystemSE.ok_s);
            }

            void rollbackOverlay(bool main, int kind)
            {
                string name = main ? MainClothesNames[kind] : SubClothesNames[kind];

                KCOXTexDataBackup.TryGetValue(name, out var tex);
                KCOXController.SetOverlayTex(tex, name);

                if (null == tex)
                {
                    Logger.Log(BepInEx.Logging.LogLevel.Debug, "[KK_SCLO] ->Overlay not found: " + name);
                }
                else
                {
                    Logger.Log(BepInEx.Logging.LogLevel.Debug, "[KK_SCLO] ->Overlay found: " + name);
                }
                return;
            }
        }

        public static readonly string[] MainClothesNames =
        {
            "ct_clothesTop",
            "ct_clothesBot",
            "ct_bra",
            "ct_shorts",
            "ct_gloves",
            "ct_panst",
            "ct_socks",
            "ct_shoes_inner",
            "ct_shoes_outer"
        };

        public static readonly string[] SubClothesNames =
        {
            "ct_top_parts_A",
            "ct_top_parts_B",
            "ct_top_parts_C"
        };

    }
}
