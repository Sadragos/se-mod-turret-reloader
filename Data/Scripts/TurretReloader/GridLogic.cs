using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Utils;

namespace TurretReloader
{
    public class GridLogic
    {
        public MyCubeGrid Grid;

        public readonly HashSet<Reloader> Reloaders = new HashSet<Reloader>();
        public readonly HashSet<IMyLargeInteriorTurret> Turrets = new HashSet<IMyLargeInteriorTurret>();

        public void Init(MyCubeGrid grid)
        {
            try
            {
                if (grid == null)
                    throw new Exception("given grid was null!");

                Grid = grid;

                // NOTE: not all blocks are fatblocks, but the kind of blocks we need are always fatblocks.
                foreach (var block in Grid.GetFatBlocks())
                {
                    BlockAdded(block);
                }

                Grid.OnFatBlockAdded += BlockAdded;
                Grid.OnFatBlockRemoved += BlockRemoved;

                Update();
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("TurretReloader: ERROR" + e.Message);
            }
        }

        public void Update()
        {
            try
            {
                if (Grid == null || Grid.IsPreview || Grid.Physics == null || !Grid.Physics.Enabled)
                    return;

                if (Reloaders.Count == 0)
                    return; // no converters, skip.

                foreach (Reloader loader in Reloaders)
                    loader.Update();

            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("TurretReloader: ERROR" + e.Message);
            }
        }

        public void Reset()
        {
            try
            {
                if (Grid != null)
                {
                    Grid.OnFatBlockAdded -= BlockAdded;
                    Grid.OnFatBlockRemoved -= BlockRemoved;
                    Grid = null;
                }

                Reloaders.Clear();
                Turrets.Clear();
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("TurretReloader: ERROR" + e.Message);
            }
        }

        private void BlockRemoved(MyCubeBlock block)
        {
            try
            {
                if (block is MyConveyorSorter)
                {
                    Reloader conv = block.GameLogic.GetAs<Reloader>();
                    if(conv != null)
                        Reloaders.Remove(conv);
                    return;
                }
                if (block is IMyLargeInteriorTurret)
                {
                    IMyLargeInteriorTurret turret = (block as IMyLargeInteriorTurret);
                    Turrets.Remove(turret);
                    return;
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("TurretReloader: ERROR" + e.Message);
            }
        }

        private void BlockAdded(MyCubeBlock block)
        {
            try
            {
                if (block is MyConveyorSorter)
                {
                    Reloader conv = block?.GameLogic?.GetAs<Reloader>();
                    if (conv != null)
                    {
                        conv.SetLogic(this);
                        Reloaders.Add(conv);
                    }
                        
                    return;
                }
                if (block is IMyLargeInteriorTurret)
                {
                    IMyLargeInteriorTurret turret = (block as IMyLargeInteriorTurret);
                    Turrets.Add(turret);
                    return;
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("TurretReloader: ERROR" + e.Message);
            }
        }
    }
}
