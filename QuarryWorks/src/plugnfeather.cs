﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;


namespace stoneworks.src
{
    class PlugnFeatherModsystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("PlugFeatherBlock", typeof(PlugnFeatherBlock));
            api.RegisterBlockEntityClass("PlugFeatherBE", typeof(PlugnFeatherBlockEntity));
        }
    }

    class PlugnFeatherBlock : Block
    {
        public static SimpleParticleProperties breakParticle2 = new SimpleParticleProperties(32, 32, ColorUtil.ColorFromRgba(122, 76, 23, 50), new Vec3d(), new Vec3d(), new Vec3f(), new Vec3f());
        public AssetLocation cracksound = new AssetLocation("game", "sounds/block/heavyice");
        public AssetLocation hammersound = new AssetLocation("game", "sounds/block/meteoriciron-hit-pickaxe");

        public int maxSearchRange = 6;
        public int workMod = 2; // the amount to multaply the amount of work needed to break the chunk.
        public int baseWork = 5; // the base amount of work needed.

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            string orientation;
            string oDirections;
            if (blockSel.Face.Index == 5)
            {
                orientation = "up";
                oDirections = SuggestedHVOrientation(byPlayer, blockSel)[0].ToString();
            }
            else if (blockSel.Face.Index == 4)
            {
                orientation = "down";
                oDirections = SuggestedHVOrientation(byPlayer, blockSel)[0].ToString();
            }
            else
            {
                orientation = "horizontal";
                oDirections = blockSel.Face.Opposite.ToString();
            }

            AssetLocation blockSwapLocation = new AssetLocation(Code.Domain, CodeWithoutParts(2) + "-" + orientation + "-" + oDirections);
            Block blockSwap = world.GetBlock(blockSwapLocation);
            world.BlockAccessor.SetBlock(blockSwap.Id, blockSel.Position);

            PlugnFeatherBlockEntity BE = world.BlockAccessor.GetBlockEntity(blockSel.Position) as PlugnFeatherBlockEntity;
            BE.orientation = orientation;
            BE.facing = oDirections;

            return true;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            PlugnFeatherBlockEntity be = world.BlockAccessor.GetBlockEntity(pos) as PlugnFeatherBlockEntity;

            if (be == null || be.master == null)
            {
                Debug.WriteLine("master is null");
                base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            }
            else
            {
                //Debug.WriteLine(world.Side);

                PlugnFeatherBlockEntity masterEntity = world.BlockAccessor.GetBlockEntity(be.master.AsBlockPos) as PlugnFeatherBlockEntity;
                ItemStack[] drops = GetDrops(world, pos, byPlayer);

                if (drops != null)
                {
                    ItemStack drop = drops[0];
                    drop.StackSize = masterEntity.slavecount + 1;
                    world.SpawnItemEntity(drop, pos.ToVec3d());
                }

                foreach (Vec3i slave in masterEntity.slaves)
                {
                    world.BlockAccessor.SetBlock(0, slave.AsBlockPos);
                    world.BlockAccessor.MarkBlockDirty(slave.AsBlockPos);
                }
                world.BlockAccessor.SetBlock(0, be.master.AsBlockPos);
                world.BlockAccessor.MarkBlockDirty(be.master.AsBlockPos);

            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            PlugnFeatherBlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as PlugnFeatherBlockEntity;
            ItemStack playerStack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;

            maxSearchRange = world.BlockAccessor.GetBlock(blockSel.Position).Attributes["searchrange"].AsInt();

            if (byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
            {

            }

            if (playerStack == null)
            {
                return false;
            }
            Item playerItem = playerStack.Item;
            if (playerItem == null)
            {
                return false;
            }
            
            if (playerStack.ItemAttributes.KeyExists("quarrystarter") && playerStack.ItemAttributes["quarrystarter"].AsBool() == true && be != null && be.master == null)
            {
                if (world.Side == EnumAppSide.Client)
                {
                    (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                }

                //Debug.WriteLine("making network");
                MakeNetwork(world, byPlayer, blockSel);
                int maxwork = WorkNeeded(be.slavecount + 1);
                be.SetMaxWork(maxwork);

                if (be.master == null)
                {
                    return false;
                }

                breakParticle2.MinPos = be.master.AsBlockPos.ToVec3d();
                breakParticle2.AddPos = new Vec3d(1, 1, 1);
                breakParticle2.ColorByBlock = world.BlockAccessor.GetBlock(be.master.AsBlockPos);
                breakParticle2.MinVelocity = new Vec3f(.1f, .3f, .1f);
                breakParticle2.AddVelocity = new Vec3f(0f, .3f, 0f);

                world.SpawnParticles(breakParticle2, byPlayer);

                foreach (Vec3i slave in be.slaves)
                {
                    breakParticle2.MinPos = slave.AsBlockPos.ToVec3d();
                    world.SpawnParticles(breakParticle2, byPlayer);

                    PlugnFeatherBlockEntity slavebe = world.BlockAccessor.GetBlockEntity(slave.AsBlockPos) as PlugnFeatherBlockEntity;
                    if (slavebe != null)
                    {
                        slavebe.SetMaxWork(maxwork);
                    }
                }
            }
            if (playerStack.ItemAttributes.KeyExists("quarryimpact") && be != null && be.master != null)
            {

                /*breakParticle2.MinPos = blockSel.Position.ToVec3d();
                breakParticle2.AddPos = new Vec3d(1, 1, 1);
                breakParticle2.ColorByBlock = world.BlockAccessor.GetBlock(blockSel.Position);
                breakParticle2.MinVelocity = new Vec3f(.1f, .3f, .1f);
                breakParticle2.AddVelocity = new Vec3f(0f, .3f, 0f);

                world.SpawnParticles(breakParticle2, byPlayer);*/



                if (be.IncreaseWork(10) == true)
                {
                    if (be.state != be.maxstate)
                    {
                        world.PlaySoundAt(cracksound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, volume: .5f);
                        Debug.WriteLine("work Maxed");
                        be.IncreaseState(1);
                        SwitchState(be.state, world, byPlayer, blockSel);   
                    }
                    if (CheckDone(be, world) == true)
                    {
                        Debug.WriteLine("all blocks at max");
                        BreakAll(be, world, byPlayer);
                    }
                }

                world.PlaySoundAt(hammersound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, volume: 0.5f);
                if (world.Side == EnumAppSide.Client)
                {
                    (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                }
                Debug.WriteLine(be.work);
            }
            
            return true;
        }


        public void MakeNetwork(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            //makes the network if a network is available to make.
            PlugnFeatherBlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as PlugnFeatherBlockEntity;

            if (be.master == null)
            {
                List<Vec3i> slaves = FindSlaves(world, byPlayer, blockSel);
                if (slaves != null)
                {
                    be.master = blockSel.Position.ToVec3i();
                    Vec3i[] cube = FindCube(slaves);
                    int work = WorkNeeded(slaves.Count + 1);

                    foreach (Vec3i slave in slaves)
                    {
                        be.addSlave(slave);
                        PlugnFeatherBlockEntity tempbe = world.BlockAccessor.GetBlockEntity(slave.AsBlockPos) as PlugnFeatherBlockEntity;
                        tempbe.master = blockSel.Position.ToVec3i();
                        tempbe.maxwork = work;
                    }
                }
            }
        }
        public int WorkNeeded(int nBlocks)
        {
            // finds the amount of work needed for each point on the network.
            return baseWork + (nBlocks * workMod);
        }

        public void SwitchState(int pickstate, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            string[] switchterm = { "one", "two", "three" };
            if (pickstate < switchterm.Count())
            {
                Block tblock = world.BlockAccessor.GetBlock(blockSel.Position);

                AssetLocation nstate = tblock.CodeWithPart(switchterm[pickstate], 2);

                (world.BlockAccessor.GetBlockEntity(blockSel.Position) as PlugnFeatherBlockEntity).state = pickstate;
                world.BlockAccessor.ExchangeBlock(world.GetBlock(nstate).Id, blockSel.Position);

                //Debug.WriteLine(nstate);
                return;
            }
            Debug.WriteLine("pickstate larger than available states");
            return;
        }

        public bool CheckDone(PlugnFeatherBlockEntity be, IWorldAccessor world)
        {
            // return false if not done, true if done.

            if (be == null || be.master == null) 
            {
                Debug.WriteLine("master or be is null");
                return false; 
            }

            PlugnFeatherBlockEntity master = world.BlockAccessor.GetBlockEntity(be.master.AsBlockPos) as PlugnFeatherBlockEntity;
            if (master.state != master.maxstate)
            {
                Debug.WriteLine("master is not maxed");
                //Debug.WriteLine(master.state);
                //Debug.WriteLine(master.maxstate);

                return false; 

            }

            List<Vec3i> slaves = master.slaves;
            foreach (Vec3i slave in slaves)
            {
                PlugnFeatherBlockEntity tempBE = world.BlockAccessor.GetBlockEntity(slave.AsBlockPos) as PlugnFeatherBlockEntity;
                if (tempBE == null)
                {
                    Debug.WriteLine(slave);
                    Debug.WriteLine(master.slavecount);
                    Debug.WriteLine("tempBE == null");
                    return false;
                }
                if (tempBE.state != tempBE.maxstate)
                {
                    Debug.WriteLine("tempBE is not maxed");
                    return false;
                }
            }
            return true;
        }
        public void BreakAll(PlugnFeatherBlockEntity be, IWorldAccessor world, IPlayer byPlayer)
        {
            PlugnFeatherBlockEntity master = world.BlockAccessor.GetBlockEntity(be.master.AsBlockPos) as PlugnFeatherBlockEntity;
            List<Vec3i> points = new List<Vec3i>();
            points.Add(be.master);
            foreach (Vec3i slave in master.slaves)
            {
                points.Add(slave);
            }

            Vec3i[] cube = FindCube(points);
            List<BlockPos> blocks = FindBlocksPos(cube[0], cube[1]);
            //world.HighlightBlocks(byPlayer, 0, blocks);
            IDictionary<string, int> stones = FindStoneTypes(world, blocks);

            string rockpath = "";
            int rockamount = 0;

            foreach (KeyValuePair<string, int> k in stones)
            {
                if (k.Value > rockamount)
                {
                    rockpath = k.Key;
                    rockamount = k.Value;
                }
            }
            //Debug.WriteLine(rockpath);

            foreach (BlockPos point in blocks)
            {
                if (world.BlockAccessor.GetBlock(point).FirstCodePart() == "rock")
                {

                    breakParticle2.ColorByBlock = world.BlockAccessor.GetBlock(point);
                    breakParticle2.MinQuantity = 5;
                    breakParticle2.AddQuantity = 2;
                    breakParticle2.MinPos = point.ToVec3d();
                    breakParticle2.AddPos = new Vec3d(1, 1, 1);
                    breakParticle2.MinVelocity = new Vec3f(0, 3, 0);
                    breakParticle2.AddVelocity = new Vec3f(.1f, .5f, .1f);
                    breakParticle2.MinSize = 1f;
                    breakParticle2.MaxSize = 3f;

                    world.BlockAccessor.SetBlock(0, point);

                    world.SpawnParticles(breakParticle2, byPlayer);
                }
            }

            // This is where our system decide what to drop.
            string dropItemFillerString = GetDropType(rockamount);
            string sizeModFillerString = GetSizeFillerString(dropItemFillerString);
            string dropItemString = "stonestorage" + dropItemFillerString + "-" + rockpath + sizeModFillerString + "north";
            Block dropItem = world.GetBlock(new AssetLocation(Code.Domain, dropItemString));
            ItemStack dropItemStack = new ItemStack(dropItem, 1);
            dropItemStack.Attributes.SetInt("stonestored", rockamount);

            world.BlockAccessor.BreakBlock(be.master.AsBlockPos, byPlayer);
            world.SpawnItemEntity(dropItemStack, new Vec3d(((cube[1].X - cube[0].X) / 2) + cube[0].X, ((cube[1].Y - cube[0].Y) / 2) + cube[0].Y, ((cube[1].Z - cube[0].Z) / 2) + cube[0].Z));

        }

        public Vec3i GetCounterpart(IWorldAccessor world, IPlayer byPlayer, BlockPos blockSel)
        {
            //looks at the blocks above and below this one it the direction it's facing and matches the first that has the orientation of up/down and the same facing direction.
            // if you're looking at this to figure out what I did.... good luck? If you can optimize this let me know. I'm moving on to something else now.
            PlugnFeatherBlockEntity BE = world.BlockAccessor.GetBlockEntity(blockSel) as PlugnFeatherBlockEntity;
            if (BE.facing == null || BE.orientation == null)
            {
                return null;
            }

            Vec3i orientation = new Vec3i(); // orientation is +\-.
            Vec3i dir = new Vec3i(); // dir is a set vector positive or negative.
            string[] checkDir = {"", "" }; // the direction string to check against.

            Vec3i checkPos = new Vec3i();
            Vec3i startPos = blockSel.ToVec3i();

            if (BE.orientation == "up")
            {
                dir = new Vec3i(0, 1, 0);
                if (BE.facing == "north" || BE.facing == "south")
                {
                    orientation = new Vec3i(0, 0, 1);
                    checkDir = new string[] { "north", "south" };
                }
                else if (BE.facing == "east" || BE.facing == "west")
                {
                    orientation = new Vec3i(1, 0, 0);
                    checkDir = new string[] { "east", "west" };
                }
            }
            else if (BE.orientation == "down")
            {
                dir = new Vec3i(0, -1, 0);
                if (BE.facing == "north" || BE.facing == "south")
                {
                    orientation = new Vec3i(0, 0, 1);
                    checkDir = new string[] { "north", "south" };
                }
                else if (BE.facing == "east" || BE.facing == "west")
                {
                    orientation = new Vec3i(1, 0, 0);
                    checkDir = new string[] { "east", "west" };
                }
            }
            else if (BE.orientation == "horizontal")
            {
                orientation = new Vec3i(0, 1, 0);
                if (BE.facing == "north")
                {
                    dir = new Vec3i(0, 0, -1);
                    checkDir = new string[] { "north", "south"};
                }
                else if (BE.facing == "east")
                {
                    dir = new Vec3i(1, 0, 0);
                    checkDir = new string[] { "east", "west" };
                }
                else if (BE.facing == "south")
                {
                    dir = new Vec3i(0, 0, 1);
                    checkDir = new string[] { "north", "south" };
                }
                else if (BE.facing == "west")
                {
                    dir = new Vec3i(-1, 0, 0);
                    checkDir = new string[] { "east", "west" };
                }
            }

            
            for (int x = 1; x <= maxSearchRange; x++)
            {
                for (int y = 1; y <= maxSearchRange; y++)
                {
                    checkPos = new Vec3i(startPos.X + (dir.X * x), startPos.Y + (dir.Y * x), startPos.Z + (dir.Z * x));

                    Vec3i rpos1 = new Vec3i(checkPos.X + (orientation.X * y), checkPos.Y + (orientation.Y * y), checkPos.Z + (orientation.Z * y));
                    Vec3i rpos2 = new Vec3i(checkPos.X - (orientation.X * y), checkPos.Y - (orientation.Y * y), checkPos.Z - (orientation.Z * y));

                    Block block1 = world.BlockAccessor.GetBlock(rpos1.AsBlockPos);
                    Block block2 = world.BlockAccessor.GetBlock(rpos2.AsBlockPos);
                    if (BE.orientation == "horizontal")
                    {
                        if (block1.Code.Path.Contains("plugnfeather") && block1.FirstCodePart(1) == this.FirstCodePart(1) && block1.Code.Path.Contains("-down-") && (block1.Code.Path.Contains(checkDir[0]) || block1.Code.Path.Contains(checkDir[1])))
                        {
                            //Debug.WriteLine(block1);
                            return rpos1;
                        }
                        if (block2.Code.Path.Contains("plugnfeather") && block2.FirstCodePart(1) == this.FirstCodePart(1) && block2.Code.Path.Contains("-up-") && (block2.Code.Path.Contains(checkDir[0]) || block2.Code.Path.Contains(checkDir[1])))
                        {
                            //Debug.WriteLine(block2);
                            return rpos2;
                        }
                    }
                    else if (BE.orientation == "up" || BE.orientation == "down")
                    {
                        if (block1.Code.Path.Contains("plugnfeather-") && block1.Code.Path.Contains("-horizontal-") && block1.FirstCodePart(1) == this.FirstCodePart(1) && (block1.Code.Path.Contains(checkDir[0]) || block1.Code.Path.Contains(checkDir[1])))
                        {
                            //Debug.WriteLine(block1);
                            return rpos1;
                        }
                        if (block2.Code.Path.Contains("plugnfeather-") && block2.Code.Path.Contains("-horizontal-") && block2.FirstCodePart(1) == this.FirstCodePart(1) && (block2.Code.Path.Contains(checkDir[0]) || block2.Code.Path.Contains(checkDir[1])))
                        {
                            //Debug.WriteLine(block2);
                            return rpos2;
                        }
                    }
                }
            }
            
            return null;
        }

        public List<Vec3i> GetNeighbours(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            //Finds blocks in a line around this block. Stops if theres a break in the line.
            PlugnFeatherBlockEntity BE = world.BlockAccessor.GetBlockEntity(blockSel.Position) as PlugnFeatherBlockEntity;
            Vec3i checkDir = new Vec3i();
            Vec3i startPos = blockSel.Position.ToVec3i();

            List<Vec3i> returnBlocks = new List<Vec3i>();

            if (BE.facing == null)
            {
                return null;
            }

            if (BE.facing == "north" || BE.facing == "south")
            {
                checkDir = new Vec3i(1, 0, 0);
            }
            else if (BE.facing == "east" || BE.facing == "west")
            {
                checkDir = new Vec3i(0, 0, 1);
            }

            bool p1 = true;
            bool p2 = true;

            for (int i = 1; i <= maxSearchRange; i++)
            {
                Vec3i checkPos1 = new Vec3i(startPos.X + (checkDir.X*i), startPos.Y + (checkDir.Y * i), startPos.Z + (checkDir.Z * i));
                Vec3i checkPos2 = new Vec3i(startPos.X - (checkDir.X*i), startPos.Y - (checkDir.Y * i), startPos.Z - (checkDir.Z * i));

                Block block1 = world.BlockAccessor.GetBlock(checkPos1.ToBlockPos());
                Block block2 = world.BlockAccessor.GetBlock(checkPos2.ToBlockPos());

                PlugnFeatherBlockEntity blocke1 = world.BlockAccessor.GetBlockEntity(checkPos1.ToBlockPos()) as PlugnFeatherBlockEntity;
                PlugnFeatherBlockEntity blocke2 = world.BlockAccessor.GetBlockEntity(checkPos2.ToBlockPos()) as PlugnFeatherBlockEntity;

                //Debug.WriteLine(FirstCodePart(1));
                if (p1 == true)
                {
                    if (block1.Code.Path.Contains(FirstCodePart()) && block1.FirstCodePart(1) == this.FirstCodePart(1) && block1.Code.Path.Contains(FirstCodePart(3)) && block1.Code.Path.Contains(FirstCodePart(4)) && blocke1.master == null)
                    {
                        //Debug.WriteLine("block1");
                        //Debug.WriteLine(checkPos1);
                        returnBlocks.Add(checkPos1);
                    }
                    else
                    {
                        p1 = false;
                    }
                }

                if (p2 == true)
                {
                    if (block2.Code.Path.Contains(FirstCodePart()) && block2.FirstCodePart(1) == this.FirstCodePart(1) && block2.Code.Path.Contains(FirstCodePart(3)) && block2.Code.Path.Contains(FirstCodePart(4)) && blocke2.master == null)
                    {
                        //Debug.WriteLine("block2");
                        //Debug.WriteLine(checkPos2);
                        returnBlocks.Add(checkPos2);
                    }
                    else
                    {
                        p2 = false;
                    }
                }

                if (returnBlocks.Count == maxSearchRange-1)
                { 
                    break;
                }
                
            }

            return returnBlocks;
        }

        public List<Vec3i> FindSlaves(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            //builds a series of points based on the surrounding blocks.
            //PlugnFeatherBlockEntity BE = world.BlockAccessor.GetBlockEntity(blockSel.Position) as PlugnFeatherBlockEntity;
            Vec3i counterpart = GetCounterpart(world, byPlayer, blockSel.Position);
            List<Vec3i> neighbours = GetNeighbours(world, byPlayer, blockSel);
            
            List<Vec3i> slaves = new List<Vec3i>();

            if (counterpart == null)
            {
                Debug.WriteLine("No Counterpart at this pos.");
                return null;
            }

            slaves.Add(counterpart);

            if (neighbours.Count == 0)
            {
                Debug.WriteLine("No Neighbours around this pos.");
                //return null;
            }
            for (int i = 0; i < neighbours.Count; i++)
            {
                PlugnFeatherBlock nblock = world.BlockAccessor.GetBlock(neighbours[i].AsBlockPos) as PlugnFeatherBlock;
                PlugnFeatherBlockEntity nblockentity = world.BlockAccessor.GetBlockEntity(neighbours[i].AsBlockPos) as PlugnFeatherBlockEntity;
                Vec3i ncounterblockpos = nblock.GetCounterpart(world, byPlayer, neighbours[i].AsBlockPos);
                PlugnFeatherBlockEntity ncounterbe = null;
                if (ncounterblockpos != null)
                {
                    ncounterbe = world.BlockAccessor.GetBlockEntity(ncounterblockpos.AsBlockPos) as PlugnFeatherBlockEntity;
                }


                if (ncounterblockpos != null && ncounterbe.master == null)
                {
                    slaves.Add(ncounterblockpos);
                    slaves.Add(neighbours[i]);
                }
            }

            for (int i = 0; i < slaves.Count; i++)
            {
                Debug.WriteLine(slaves[i]);
            }
            return slaves;

        }

        public Vec3i[] FindCube(List<Vec3i> dataPoints)
        {
            if (dataPoints == null)
            {
                return null;
            }
            Vec3i minPos = dataPoints[0];
            Vec3i maxPos = dataPoints[0];

            foreach (Vec3i pos in dataPoints)
            {
                if (pos.X < minPos.X)
                {
                    minPos = new Vec3i(pos.X, minPos.Y, minPos.Z);
                }
                if (pos.Y < minPos.Y)
                {
                    minPos = new Vec3i(minPos.X, pos.Y, minPos.Z);
                }
                if (pos.Z < minPos.Z)
                {
                    minPos = new Vec3i(minPos.X, minPos.Y, pos.Z);
                }

                if (pos.X > maxPos.X)
                {
                    maxPos = new Vec3i(pos.X, maxPos.Y, maxPos.Z);
                }
                if (pos.Y > maxPos.Y)
                {
                    maxPos = new Vec3i(maxPos.X, pos.Y, maxPos.Z);
                }
                if (pos.Z > maxPos.Z)
                {
                    maxPos = new Vec3i(maxPos.X, maxPos.Y, pos.Z);
                }
            }
            Vec3i[] rvecs = { minPos, maxPos };
            return rvecs;
        }

        public List<BlockPos> FindBlocksPos(Vec3i pmin, Vec3i pmax)
        {
            // returns a list of all blocks found in a cube defined by two points.

            List<BlockPos> bps = new List<BlockPos>();

            for (int x = pmin.X; x <= pmax.X; x++)
            {
                for (int y = pmin.Y; y <= pmax.Y; y++)
                {
                    for (int z = pmin.Z; z <= pmax.Z; z++)
                    {
                        bps.Add(new BlockPos(x, y, z));
                    }
                }
            }

            return bps;
        }


        public int FindAmountOfStone(IWorldAccessor world, List<BlockPos> points)
        {
            int rint = 0;
            foreach (BlockPos bp in points)
            {
                if (world.BlockAccessor.GetBlock(bp).FirstCodePart() == "rock")
                {
                    rint += 1;
                }
            }
            return rint;
        }

        public IDictionary<string, int> FindStoneTypes(IWorldAccessor world, List<BlockPos> points)
        {
            // Checks all points givin and returns the types and quantaties of stone found.
            IDictionary<string, int> rdict = new Dictionary<string, int>();
            foreach (BlockPos bp in points)
            {
                Block tblock = world.BlockAccessor.GetBlock(bp);
                if (tblock.FirstCodePart() == "rock")
                {
                    if (rdict.ContainsKey(tblock.FirstCodePart(1)))
                    {
                        rdict[tblock.FirstCodePart(1)] += 1;
                    }
                    else
                    {
                        rdict.Add(tblock.FirstCodePart(1), 1);
                    }
                }
            }
            return rdict;
        }


        public string GetSizeFillerString(string rocksize)
        {
            string rstring = "-";
            
            if (rocksize == "Large")
            {
                rstring = "-zero-";
            }
            else if (rocksize == "Huge")
            {
                rstring = "-zero-zero-";
            }
            else if (rocksize == "Gient")
            {
                rstring = "-zero-zero-";
            }

            return rstring;
        }

        public string GetDropType(int quantity)
        {
            string rstring = null;
            if (quantity > 0 && quantity < 42)
            {
                rstring = "Small";
            }
            else if (quantity >= 42 && quantity < 84)
            {
                rstring = "Med";
            }
            else if (quantity >= 84 && quantity < 126)
            {
                rstring = "Large";
            }
            else if (quantity >= 126 && quantity < 168)
            {
                rstring = "Huge";
            }
            else if (quantity >= 168)
            {
                rstring = "Gient";
            }
            return rstring;
        }
    }

    class PlugnFeatherBlockEntity : BlockEntity
    {
        public Vec3i master; // Position of master block.
        public List<Vec3i> slaves = new List<Vec3i>(); // Positions of slave blocks.
        public int slavecount = 0;

        public string orientation; // Orientation that this block is facing. Up, Down, Horizontal.
        public string facing; // Direction that this block is facing. North, South, East, West.

        public int state = 0;
        public int maxstate = 2;
        public int work = 0;
        public int maxwork = 100;


        public bool SetWork(int n)
        {
            //sets work between 0 and max work.
            if (n >= 0 && n <= maxwork)
            {
                work = n;
                return true;
            }
            return false;
        }
        public bool IncreaseWork(int n)
        {
            //Increases the amount of work, if the amount is more than the max allowed will return true to allow for the calling method to change the state.
            if (n == 0)
            {
                return false;
            }

            if (work + n > maxwork)
            {
                work += (n - maxwork);
                return true;
            }

            work += n;
            return false;
        }
        public bool SetMaxWork(int n)
        {
            if (n >= 0)
            {
                maxwork = n;
                return true;
            }
            return false;
        }

        public bool IncreaseState(int n)
        {
            if (n == 0)
            {
                return false;
            }
            if (state + n > maxstate)
            {
                state = maxstate;
                return true;
            }
            state += 1;

            return true;
        }
        public bool SetState(int n)
        { 
            // returns false if n is out of range.
            if (n <= maxstate && n >= 0)
            {
                state = n;
                return true;
            }
            return false;
        }


        public bool addSlave(Vec3i slave)
        {
            if (slave == null)
            {
                return false;
            }
            slaves.Add(slave);
            slavecount = slaves.Count;
            return true;
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetInt("slavecount", slavecount);
            tree.SetString("orientation", orientation);
            tree.SetString("facing", facing);

            tree.SetInt("state", state);
            tree.SetInt("work", work);
            tree.SetInt("maxwork", maxwork);

            if (slaves.Count != 0)
            {
                for (int i = 0; i < slaves.Count; i++)
                {
                    string savestringx = "slave" + i + "x";
                    string savestringy = "slave" + i + "y";
                    string savestringz = "slave" + i + "z";

                    tree.SetInt(savestringx, slaves[i].X);
                    tree.SetInt(savestringy, slaves[i].Y);
                    tree.SetInt(savestringz, slaves[i].Z);
                }
            }
            if (master != null)
            {
                tree.SetInt("masterx", master.X);
                tree.SetInt("mastery", master.Y);
                tree.SetInt("masterz", master.Z);
            }

            base.ToTreeAttributes(tree);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            slavecount = tree.GetInt("slavecount");
            orientation = tree.GetString("orientation");
            facing = tree.GetString("facing");

            state = tree.GetInt("state", state);
            work = tree.GetInt("work", work);
            maxwork = tree.GetInt("maxwork", maxwork);

            if (slavecount != 0)
            {
                for (int i = 0; i < slavecount; i++)
                {
                    slaves.Add(new Vec3i(tree.GetInt("slave" + i + "x"), tree.GetInt("slave" + i + "y"), tree.GetInt("slave" + i + "z")));
                }
            }
            if (tree.HasAttribute("masterx"))
            {
                master = new Vec3i(tree.GetInt("masterx"), tree.GetInt("mastery"), tree.GetInt("masterz"));
            }



            base.FromTreeAttributes(tree, worldAccessForResolve);
        }
    }
}
