﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Contracts;
using KSP.UI.Screens;
using RSTUtils;

namespace ResearchBodies
{
    public partial class ResearchBodiesController : MonoBehaviour
    {
        internal bool isTSTInstalled = false;
        internal bool isPCBMInstalled = false;
        
        public static ResearchBodiesController instance;
        
        public void Awake()
        {
            instance = this;
            _startwindowId = Utilities.getnextrandomInt();
            _hoverwindowId = Utilities.getnextrandomInt();
            _RBwindowId = Utilities.getnextrandomInt();
            _settingswindowId = Utilities.getnextrandomInt();

            RBMenuAppLToolBar = new AppLauncherToolBar("ResearchBodies", "ResearchBodies",
                Textures.PathToolbarIconsPath + "/RBToolBaricon",
                ApplicationLauncher.AppScenes.SPACECENTER,
                (Texture)Textures.ApplauncherIcon, (Texture)Textures.ApplauncherIcon,
                GameScenes.SPACECENTER);
        }

        public void Start()
        {
            isTSTInstalled = Database.instance.isTSTInstalled;
            isPCBMInstalled = Utilities.IsPCBMInstalled;
            if (isPCBMInstalled)  //If Progressive CB Maps assembly is present, initialise PCBM wrapper.
            {
                PCBMWrapper.InitPCBMWrapper();
                if (!PCBMWrapper.APIPCBMReady)
                {
                    isPCBMInstalled = false; //If the initialise of wrapper failed set bool to false, we won't be interfacing to PCBM today.
                }
            }
            if (!Database.instance.enableInSandbox && (HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX || HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX))
                enable = false;
            
            //Create Instructor
            _instructor = Create("Instructor_Wernher");
            
            //Register for Contract On offerred so we can remove ones that are for bodies not yet tracked.
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
                GameEvents.Contract.onOffered.Add(CheckContracts);

            //If RB is enabled set initial Discovery Levels of CBs and call ProgressiveCBMaps to set their graphics levels.
            if (enable)
            {
                SetBodyDiscoveryLevels();
                
                if (!ToolbarManager.ToolbarAvailable && !Database.instance.UseAppLauncher)
                {
                    Database.instance.UseAppLauncher = true;
                }
                RBMenuAppLToolBar.Start(Database.instance.UseAppLauncher);
                GameEvents.onGUIRnDComplexSpawn.Add(TurnUIOff);
                GameEvents.onGUIMissionControlSpawn.Add(TurnUIOff);
                GameEvents.onGUIAstronautComplexSpawn.Add(TurnUIOff);
                GameEvents.onGUIAdministrationFacilitySpawn.Add(TurnUIOff);
                GameEvents.onGUIRnDComplexDespawn.Add(TurnUIOn);
                GameEvents.onGUIMissionControlDespawn.Add(TurnUIOn);
                GameEvents.onGUIAstronautComplexDespawn.Add(TurnUIOn);
                GameEvents.onGUIAdministrationFacilityDespawn.Add(TurnUIOn);
                Utilities.setScaledScreen();

                difficulty = ResearchBodies.Instance.RBgameSettings.Difficulty;
                ResearchCost = ResearchBodies.Instance.RBgameSettings.ResearchCost;
                ScienceReward = ResearchBodies.Instance.RBgameSettings.ScienceReward;
                ProgressResearchCost = ResearchBodies.Instance.RBgameSettings.ProgressResearchCost;
            }
        }

        public void OnDestroy()
        {
            if (_portrait != null)
                _portrait.Release();

            if (_instructor != null)
                Destroy(_instructor.gameObject);
            if (enable)
                RBMenuAppLToolBar.Destroy();
            GameEvents.onGUIRnDComplexDespawn.Remove(TurnUIOff);
            GameEvents.onGUIMissionControlDespawn.Remove(TurnUIOff);
            GameEvents.onGUIAstronautComplexSpawn.Remove(TurnUIOff);
            GameEvents.onGUIAdministrationFacilitySpawn.Remove(TurnUIOff);
            GameEvents.onGUIRnDComplexDespawn.Remove(TurnUIOn);
            GameEvents.onGUIMissionControlDespawn.Remove(TurnUIOn);
            GameEvents.onGUIAstronautComplexDespawn.Remove(TurnUIOn);
            GameEvents.onGUIAdministrationFacilityDespawn.Remove(TurnUIOn);
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
                GameEvents.Contract.onOffered.Remove(CheckContracts);
        }
        
        /// <summary>
        /// Called by GameEvent onOffered. Will check contract parameters for reference to untracked bodies.
        /// If it finds a reference it will Withdraw the contract.
        /// </summary>
        /// <param name="contract"></param>
        private void CheckContracts(Contract contract)
        {
            
            foreach (ContractParameter cp in contract.AllParameters.ToList())
            {

                foreach (KeyValuePair<CelestialBody, CelestialBodyInfo> body in Database.instance.CelestialBodies) 
                {
                        if (!Database.instance.CelestialBodies[body.Key].isResearched && cp.Title.Contains(body.Key.GetName()))
                        {
                            TryWithDrawContract(contract);
                            break;
                        }
                    
                }
            }
            
        }
        private void TryWithDrawContract(Contract c)
        {
            try
            {
                RSTLogWriter.Log("WithDrew contract \"{0}\"" , c.Title);
                c.Withdraw(); //Changed to Withdraw - this will not penalize reputation.
            }
            catch (Exception e)
            {
                RSTLogWriter.Log("Unable to Withraw contract ! {0}" , e);
            }
        }

        /// <summary>
        /// Returns True if the Trackstation is Level 1 otherwise False.
        /// </summary>
        public bool IsTSlevel1
        {
            get { return PSystemSetup.Instance.GetSpaceCenterFacility("TrackingStation").GetFacilityLevel() < 0.5; }
        }
        
        public static bool Research(CelestialBody body, int researchToAdd)
        {
            if (Database.instance.CelestialBodies[body].researchState < 100)
            {
                if (Funding.Instance != null)
                {
                    if (Funding.Instance.Funds >= ResearchBodies.Instance.RBgameSettings.ProgressResearchCost)
                    {
                        Database.instance.CelestialBodies[body].researchState += researchToAdd;
                        Funding.Instance.AddFunds(-ResearchBodies.Instance.RBgameSettings.ProgressResearchCost, TransactionReasons.None);
                    }
                    else
                    {
                        ScreenMessages.PostScreenMessage(string.Format(Locales.currentLocale.Values["funds_notEnough"]), 3.0f, ScreenMessageStyle.UPPER_CENTER);
                    }
                }
                else
                {
                    if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER)
                    {
                        Database.instance.CelestialBodies[body].researchState += researchToAdd;
                    }
                }
                KeyValuePair<CelestialBody, CelestialBodyInfo> cb =
                                    new KeyValuePair<CelestialBody, CelestialBodyInfo>(body,
                                        Database.instance.CelestialBodies[body]);
                ResearchBodiesController.instance.SetIndividualBodyDiscoveryLevel(cb);
                if (Database.instance.CelestialBodies[body].researchState == 100 && ResearchAndDevelopment.Instance != null)
                {
                    ScreenMessages.PostScreenMessage(string.Format(Locales.currentLocale.Values["research_isNowFullyResearched_funds"], body.GetName(), ResearchBodies.Instance.RBgameSettings.ScienceReward), 5f);
                    ResearchAndDevelopment.Instance.AddScience(ResearchBodies.Instance.RBgameSettings.ScienceReward, TransactionReasons.None);
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        public static void LaunchResearchPlan(CelestialBody cb)
        {
            if (Database.instance.CelestialBodies[cb].researchState == 0)
            {
                if (Funding.Instance != null)
                {
                    if (Funding.Instance.Funds >= ResearchBodies.Instance.RBgameSettings.ResearchCost)
                    {
                        Funding.Instance.AddFunds(-ResearchBodies.Instance.RBgameSettings.ResearchCost, TransactionReasons.None);
                        Research(cb, 10);
                    }
                    else
                        ScreenMessages.PostScreenMessage(
                            string.Format(Locales.currentLocale.Values["launchPlan_notEnoughScience"], cb.GetName()),
                            3.0f, ScreenMessageStyle.UPPER_CENTER);
                }
                else
                {
                    if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER)
                    {
                        Research(cb, 10);
                    }
                }
            }
            else
                RSTLogWriter.Log(string.Format(Locales.currentLocale.Values["launchPlan_alreadyStarted"], cb.GetName()));
        }
        public static void StopResearchPlan(CelestialBody cb)
        {
            if (Database.instance.CelestialBodies[cb].researchState >= 10)
            {
                if (Funding.Instance != null)
                {
                    Funding.Instance.AddFunds(ResearchBodies.Instance.RBgameSettings.ResearchCost, TransactionReasons.None);
                }
                Database.instance.CelestialBodies[cb].researchState = 0;
                KeyValuePair<CelestialBody, CelestialBodyInfo> cbd =
                                    new KeyValuePair<CelestialBody, CelestialBodyInfo>(cb,
                                        Database.instance.CelestialBodies[cb]);
                ResearchBodiesController.instance.SetIndividualBodyDiscoveryLevel(cbd);
            }
            else
                RSTLogWriter.Log(string.Format(Locales.currentLocale.Values["stopPlan_hasntBeenStarted"], cb.GetName()));
        }

        /// <summary>
        /// Set Discovery Levels of the Bodies
        /// None = 0, Presence = 1 (Object has been detected in tracking station), Name = 4 (Object has been tracked), StateVectors = 8 (Object is currently tracked),
        /// Appearance = 16 (Unlocks mass and type fields; intended for discoverable CelestialBodies?)
        /// </summary>
        public void SetBodyDiscoveryLevels()
        {
            foreach (KeyValuePair<CelestialBody, CelestialBodyInfo> cb in Database.instance.CelestialBodies)
            {
                SetIndividualBodyDiscoveryLevel(cb);
            }
        }

        public void SetIndividualBodyDiscoveryLevel(KeyValuePair<CelestialBody, CelestialBodyInfo> cb)
        {
            if (!cb.Value.ignore)
            {
                if (!cb.Value.isResearched)
                {
                    SetBodyDiscoveryLevel(cb, DiscoveryLevels.Presence);
                }
                else if (cb.Value.isResearched && cb.Value.researchState < 50)
                {
                    SetBodyDiscoveryLevel(cb, DiscoveryLevels.Appearance);
                }
                else
                {
                    SetBodyDiscoveryLevel(cb, DiscoveryLevels.Owned);
                }
            }
            else
            {
                SetBodyDiscoveryLevel(cb, DiscoveryLevels.Owned);
            }
        }

        public void SetBodyDiscoveryLevel(KeyValuePair<CelestialBody, CelestialBodyInfo> cb, DiscoveryLevels level)
        {
            cb.Key.DiscoveryInfo.SetLevel(level);
            try
            {
                if (Database.instance.CelestialBodies[cb.Key.referenceBody].KOPbarycenter)
                    cb.Key.referenceBody.DiscoveryInfo.SetLevel(level);
                if (cb.Value.KOPrelbarycenterBody != null)
                    cb.Value.KOPrelbarycenterBody.DiscoveryInfo.SetLevel(level);
            }
            catch (Exception)
            {// throw;
            }
            if (isPCBMInstalled  && (HighLogic.LoadedScene == GameScenes.FLIGHT || HighLogic.LoadedScene == GameScenes.TRACKSTATION))  //If progressive CB maps are installed set the level of the meshmap.
            {
                if (cb.Value.ignore)
                    return;

                if (!cb.Value.isResearched)
                {
                    SetBodyProgressiveCBMap(cb.Key, 1);
                }
                else
                {
                    if (cb.Value.researchState < 30)
                    {
                        SetBodyProgressiveCBMap(cb.Key, 2);
                    }
                    else
                    {
                        if (cb.Value.researchState < 50)
                        {
                            SetBodyProgressiveCBMap(cb.Key, 3);
                        }
                        else
                        {
                            if (cb.Value.researchState < 70)
                            {
                                SetBodyProgressiveCBMap(cb.Key, 4);
                            }
                            else
                            {
                                if (cb.Value.researchState < 90)
                                {
                                    SetBodyProgressiveCBMap(cb.Key, 5);
                                }
                                else
                                {
                                    SetBodyProgressiveCBMap(cb.Key, 6);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Set Body Graphics levels in ProgressiveCBMaps
        /// </summary>
        public void SetBodyProgressiveCBMap(CelestialBody cb, int level)
        {
            if (PCBMWrapper.actualPCBMAPI.CBVisualMapsInfo != null)
            {
                if (PCBMWrapper.actualPCBMAPI.CBVisualMapsInfo.ContainsKey(cb))
                {
                    PCBMWrapper.actualPCBMAPI.CBVisualMapsInfo[cb].setVisualLevel(level);
                }
            }
        }
        
    }
    /*
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class ResearchBodies_Observatory : MonoBehaviour
    {
        Collider ObservatoryCollid;
        public bool observatoryCloned = false;

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    Log.log("Object : distance : " + hit.distance + ", name: " + hit.transform.name + ", gameObject name : " + hit.transform.gameObject.name);
                }
            }
            if (!observatoryCloned)
            {
                ObservatoryCollid = FindObjectsOfType<Collider>().FirstOrDefault(collider => collider.name.Contains("Observatory_Mesh"));
                if (ObservatoryCollid != null)
                {
                    //  GameObject obj = new GameObject("Observatory_ResearchBodies");
                    Instantiate(ObservatoryCollid.transform, new Vector3(215f, -362f, 460f), ObservatoryCollid.transform.rotation);
                    //   NewObserv.transform.position = new Vector3(215f, -362f, 460f);
                    // NewObserv.transform.parent = null;
                    Log.log("Cloned observatory mesh");
                    observatoryCloned = true;
                }
            }
        }
    } */
}
