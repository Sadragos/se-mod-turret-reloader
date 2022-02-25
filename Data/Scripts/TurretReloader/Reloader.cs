using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.GameSystems.Conveyors;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;

using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRage.Game.Entity;
using VRage.Voxels;
using SpaceEngineers.Game.ModAPI;
using Sandbox.Game.Entities.Cube;

namespace TurretReloader
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "TurretReloader")]
    public class Reloader : MyGameLogicComponent
    {
        public static float POWER_IDLE = 0.1f;
        public static float POWER_WORK = 1f;

        bool _init = false;
        public MyConveyorSorter Block;
        public Sandbox.ModAPI.IMyFunctionalBlock TerminalBlock;
        public GridLogic Logic;
        private string LastTurret;
        private bool LastTurretIdle = true;
        private bool Transported = false;
        private int AmmoCount;
        private Vector3D EffectPosition = Vector3D.Zero;
        private MyResourceSinkComponent resourceSink;

        private MyObjectBuilder_AmmoMagazine MagNew = new MyObjectBuilder_AmmoMagazine() { SubtypeName = "RapidFireAutomaticRifleGun_Mag_50rd" };
        private MyObjectBuilder_AmmoMagazine MagOld = new MyObjectBuilder_AmmoMagazine() { SubtypeName = "NATO_5p56x45mm" };

        public float ItemsPerMWs;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            Block = (MyConveyorSorter)Entity;
            TerminalBlock = (Sandbox.ModAPI.IMyFunctionalBlock)Entity;
            TerminalBlock.AppendingCustomInfo += AppendingCustomInfo;
            MyLog.Default.WriteLine("TurretReloader: DEBUG init");

            resourceSink = TerminalBlock.ResourceSink as MyResourceSinkComponent;
            if (resourceSink != null)
            {
                resourceSink.SetMaxRequiredInputByType(ElectricityId, POWER_WORK);
                resourceSink.SetRequiredInputFuncByType(ElectricityId, ComputeRequiredElectricPower);
                resourceSink.Update();
            }
        }


        public override void Close()
        {
            Logic = null;
            TerminalBlock.AppendingCustomInfo -= AppendingCustomInfo;
        }

        public void SetLogic(GridLogic logic)
        {
            Logic = logic;
        }

        void AppendingCustomInfo(Sandbox.ModAPI.IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                if (!TerminalBlock.Enabled)
                {
                    stringBuilder
                        .Append("Currently Disabled.\n");
                }
                else
                {
                    if (Logic.Turrets.Count > 0)
                    {
                        stringBuilder
                            .Append("Reloading ")
                            .Append(Logic.Turrets.Count)
                            .Append(" Interior Turrets.\n")
                            .Append(AmmoCount)
                            .Append(" Magazines available.\n");
                    }
                    else
                    {
                        stringBuilder.Append("No Interior Turrets found.\n");
                    }
                    if (LastTurret != null)
                    {
                        stringBuilder
                            .Append("-> ")
                            .Append(LastTurret)
                            .Append(LastTurretIdle ? " checked." : " reloaded.");
                    }
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("TurretReloader: ERROR " + e);
            }
        }

        public static readonly MyDefinitionId ElectricityId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
        private float ComputeRequiredElectricPower()
        {
            if (TerminalBlock == null) return 0f;
            var required = 0f;
            if (TerminalBlock.IsFunctional && TerminalBlock.Enabled && Logic != null)
            {
                required += !Transported || Logic.Turrets.Count == 0 ? POWER_IDLE : POWER_WORK;
            }
            return required;
        }

        internal void Update()
        {
            if (TerminalBlock != null && TerminalBlock.IsFunctional && TerminalBlock.Enabled)
            {
                IMyInventory inven = TerminalBlock.GetInventory(0);
                int MagCountNew = (int)inven.GetItemAmount(MagNew);
                int MagCountOld = (int)inven.GetItemAmount(MagOld);
                AmmoCount = MagCountNew + MagCountOld;
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    Transported = false;
                    if (AmmoCount > 0)
                    {
                        LastTurretIdle = true;
                        int selectedTurret = MyUtils.GetRandomInt(0, Logic.Turrets.Count);
                        IMyLargeInteriorTurret turret = Logic.Turrets.ElementAt(selectedTurret);
                        IMyInventory turrentInven = turret.GetInventory(0);
                        LastTurret = turret.DisplayNameText;
                        if (!turret.Closed && !turret.MarkedForClose && turret.IsFunctional)
                        {
                            int turretAmount = (int)turrentInven.GetItemAmount(MagNew) + (int)turrentInven.GetItemAmount(MagOld);
                            if (turretAmount < 10)
                            {
                                if (MagCountOld > 0)
                                {
                                    inven.RemoveItemsOfType(1, MagOld);
                                    turrentInven.AddItems(1, MagOld);
                                }
                                else if (MagCountNew > 0)
                                {
                                    inven.RemoveItemsOfType(1, MagNew);
                                    turrentInven.AddItems(1, MagNew);
                                }
                                AmmoCount--;
                                turret.SlimBlock.ComputeWorldCenter(out EffectPosition);
                                MyVisualScriptLogicProvider.CreateParticleEffectAtPosition(MyParticleEffectsNameEnum.Smoke_Collector, EffectPosition);
                                MyVisualScriptLogicProvider.PlaySingleSoundAtPosition("AmmoTeleport", EffectPosition);
                                TerminalBlock.SlimBlock.ComputeWorldCenter(out EffectPosition);
                                MyVisualScriptLogicProvider.PlaySingleSoundAtPosition("AmmoTeleport", EffectPosition);
                                LastTurretIdle = false;
                                Transported = true;
                            }
                        }
                    }
                }
            }
            resourceSink.Update();
            TerminalBlock.RefreshCustomInfo();
            RefreshControls(TerminalBlock);
        }

        public static void RefreshControls(Sandbox.ModAPI.IMyFunctionalBlock block)
        {

            if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                var myCubeBlock = block as MyCubeBlock;

                if (myCubeBlock.IDModule != null)
                {
                    var share = myCubeBlock.IDModule.ShareMode;
                    var owner = myCubeBlock.IDModule.Owner;
                    myCubeBlock.ChangeOwner(owner, share == MyOwnershipShareModeEnum.None ? MyOwnershipShareModeEnum.All : MyOwnershipShareModeEnum.None);
                    myCubeBlock.ChangeOwner(owner, share);
                }
            }
        }
    }
}