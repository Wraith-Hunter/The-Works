using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API;


namespace stoneworks.src
{
    class StoneStorageModsystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("RubbleStorage", typeof(RubbleStorage));
            api.RegisterBlockEntityClass("RubbleStorageBE", typeof(RubbleStorageBE));

            api.RegisterBlockClass("RoughStoneStorage", typeof(RoughCutStorage));
            api.RegisterBlockEntityClass("StoneStorageCoreBE", typeof(RoughCutStorageBE));
            api.RegisterBlockEntityClass("StoneStorageCapBE", typeof(GenericStorageCapBE));
        }
    }

    class GenericStoneStorage: Block
    {
        static SimpleParticleProperties breakparticle = new SimpleParticleProperties()
        {
            MinPos = new Vec3d(),
            AddPos = new Vec3d(),
            MinQuantity = 0,
            AddQuantity = 3,
            GravityEffect = 1f,
            WithTerrainCollision = true,
            ParticleModel = EnumParticleModel.Quad,
            LifeLength = 0.5f,
            MinVelocity = new Vec3f(-1, 2, -1),
            AddVelocity = new Vec3f(2, 0, 2),
            MinSize = 0.07f,
            MaxSize = 0.1f,
        };

        static GenericStoneStorage()
        {
            breakparticle.ParticleModel = EnumParticleModel.Quad;
            breakparticle.AddPos.Set(1, 1, 1);
            breakparticle.MinQuantity = 2;
            breakparticle.AddQuantity = 12;
            breakparticle.LifeLength = 2f;
            breakparticle.MinSize = 0.2f;
            breakparticle.MaxSize = 0.5f;
            breakparticle.MinVelocity.Set(-0.4f, -0.4f, -0.4f);
            breakparticle.AddVelocity.Set(0.8f, 1.2f, 0.8f);
            breakparticle.DieOnRainHeightmap = false;
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            BlockFacing[] sughv = SuggestedHVOrientation(byPlayer, blockSel);
            Block ablock = world.GetBlock(CodeWithVariant("dir", sughv[0].Code));

            if (ablock.Attributes != null && ablock.Attributes.KeyExists("caps"))
            {
                for (int i = 0; i < ablock.Attributes["caps"].AsArray().Length; i++)
                {
                    BlockPos checkspot = blockSel.Position.Copy() + new BlockPos(ablock.Attributes["caps"].AsArray()[i]["x"].AsInt(), ablock.Attributes["caps"].AsArray()[i]["y"].AsInt(), ablock.Attributes["caps"].AsArray()[i]["z"].AsInt());
                    if (world.BlockAccessor.GetBlock(checkspot).Id != 0)
                    {
                        return false;
                    }
                }
            }
            return base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode);
        }
        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            BlockFacing[] sughv = SuggestedHVOrientation(byPlayer, blockSel);

            Block ablock = world.GetBlock(CodeWithVariant("dir", sughv[0].Code));
            world.BlockAccessor.SetBlock(ablock.Id, blockSel.Position);

            world.BlockAccessor.MarkBlockDirty(blockSel.Position);
            world.BlockAccessor.MarkBlockEntityDirty(blockSel.Position);

            if (ablock.Attributes != null && ablock.Attributes.KeyExists("caps"))
            {
                for (int i = 0; i < ablock.Attributes["caps"].AsArray().Length; i++)
                {
                    Dictionary<string, string> rdict = new Dictionary<string, string>();

                    foreach (JsonObject obj in ablock.Attributes["caps"].AsArray()[i]["varType"].AsArray())
                    {
                        rdict.Add(obj.AsArray()[0].AsString(), obj.AsArray()[1].AsString());
                    }  

                    //Block capBlock = world.GetBlock(CodeWithVariant("dir", ablock.Attributes["caps"].AsArray()[0]["var"].AsString()));
                    Block capBlock = world.GetBlock(CodeWithVariants(rdict));
                    BlockPos capPos = blockSel.Position.Copy() + new BlockPos(ablock.Attributes["caps"].AsArray()[i]["x"].AsInt(), ablock.Attributes["caps"].AsArray()[i]["y"].AsInt(), ablock.Attributes["caps"].AsArray()[i]["z"].AsInt());
                    world.BlockAccessor.ExchangeBlock(capBlock.Id, capPos);

                    world.BlockAccessor.SpawnBlockEntity("StoneStorageCapBE", capPos);
                    (world.BlockAccessor.GetBlockEntity(capPos) as GenericStorageCapBE).core = blockSel.Position;
                    (world.BlockAccessor.GetBlockEntity(blockSel.Position) as GenericStorageCoreBE).caps.Add(capPos);

                    world.BlockAccessor.MarkBlockDirty(capPos);
                    world.BlockAccessor.MarkBlockEntityDirty(capPos);
                }
            }
            return true;
        }
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            BlockPos masterpos = null;

            if (be is GenericStorageCapBE)
            {
                masterpos = (be as GenericStorageCapBE).core;
                be = world.BlockAccessor.GetBlockEntity((be as GenericStorageCapBE).core);
            }
            else
            {
                masterpos = pos;
            }

            if (be == null)
            {
                world.BlockAccessor.SetBlock(0, pos);
                return;
            }

            GenericStorageCoreBE core = be as GenericStorageCoreBE;
            foreach (BlockPos cap in core.caps)
            {
                breakparticle.MinPos = cap.ToVec3d();
                breakparticle.ColorByBlock = world.BlockAccessor.GetBlock(masterpos);
                world.BlockAccessor.SetBlock(0, cap);
                world.BlockAccessor.RemoveBlockEntity(cap);
                world.SpawnParticles(breakparticle, byPlayer);
            }
            
            world.BlockAccessor.SetBlock(0, masterpos);
            
            //base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public bool SwitchVariant(IWorldAccessor world, BlockSelection blockSel, Dictionary<string, string> switchArray)
        {
            //Switches multiblock to a seperate variants.
            Block tempv = world.GetBlock(CodeWithVariants(switchArray));
            BlockPos corpos = blockSel.Position;

            List<BlockPos> hlightspots = new List<BlockPos>();
            if (tempv != null)
            {
                BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
                if (be is GenericStorageCapBE)
                {
                    blockSel.Position = (be as GenericStorageCapBE).core;
                    (world.BlockAccessor.GetBlock((be as GenericStorageCapBE).core) as GenericStoneStorage).SwitchVariant(world, blockSel, switchArray);
                    return true;
                }


                foreach (BlockPos slave in (be as GenericStorageCoreBE).caps)
                {
                    world.BlockAccessor.SetBlock(0, slave);
                }
                (be as GenericStorageCoreBE).caps = new List<BlockPos>();


                Block bactual = world.BlockAccessor.GetBlock(corpos);
                Block bfull = world.GetBlock(bactual.CodeWithVariants(switchArray));

                if (tempv.Attributes != null && tempv.Attributes.KeyExists("caps") && bfull != null)
                {
                    world.BlockAccessor.ExchangeBlock(bfull.Id, corpos);
                    foreach (JsonObject b in tempv.Attributes["caps"].AsArray())
                    {
                        Dictionary<string, string> rdict = new Dictionary<string, string>();
                        foreach (JsonObject bv in b["varType"].AsArray())
                        {
                            rdict.Add(bv.AsArray()[0].AsString(), bv.AsArray()[1].AsString());
                        }
                        Block capblock = world.GetBlock(bfull.CodeWithVariants(rdict));
                        BlockPos capspot = new BlockPos(b["x"].AsInt(), b["y"].AsInt(), b["z"].AsInt()) + corpos;
                        world.BlockAccessor.ExchangeBlock(capblock.Id, capspot);
                        world.BlockAccessor.SpawnBlockEntity("StoneStorageCapBE", capspot);
                        (world.BlockAccessor.GetBlockEntity(capspot) as GenericStorageCapBE).core = corpos;
                        (be as GenericStorageCoreBE).caps.Add(capspot);
                    }
                }
            }
                
            return true;
        }
    }

    class GenericStorageCoreBE : BlockEntity
    {
        public List<BlockPos> caps = new List<BlockPos>(); // where the other half of this structure is.
       
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            if (tree.HasAttribute("capCount"))
            {
                for (int i = 0; i < tree.GetInt("capCount"); i++)
                {
                    caps.Add(new BlockPos(tree.GetInt("cap" + i + "x"), tree.GetInt("cap" + i + "y"), tree.GetInt("cap" + i + "z")));
                }
            }
            base.FromTreeAttributes(tree, worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            if (caps.Count > 0)
            {
                tree.SetInt("capCount", caps.Count);
                for(int i=0; i < caps.Count; i++)
                {
                    tree.SetInt("cap"+ i +"x", caps[i].X);
                    tree.SetInt("cap"+i+"y", caps[i].Y);
                    tree.SetInt("cap"+i+"z", caps[i].Z);
                }
            }
            base.ToTreeAttributes(tree);
        }
    }

    class GenericStorageCapBE : BlockEntity
    {
        public BlockPos core = null;

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            if (tree.HasAttribute("capx"))
            {
                core = new BlockPos(tree.GetInt("capx"), tree.GetInt("capy"), tree.GetInt("capz"));
            }
            base.FromTreeAttributes(tree, worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            if (core != null)
            {
                tree.SetInt("capx", core.X);
                tree.SetInt("capy", core.Y);
                tree.SetInt("capz", core.Z);
            }
            base.ToTreeAttributes(tree);
        }
    }


    class RoughCutStorage : GenericStoneStorage
    {
        //used to store what is dropped from the quarry.

        static SimpleParticleProperties interactParticles = new SimpleParticleProperties()
        {
            MinPos = new Vec3d(),
            AddPos = new Vec3d(),
            MinQuantity = 0,
            AddQuantity = 3,
            GravityEffect = .9f,
            WithTerrainCollision = true,
            ParticleModel = EnumParticleModel.Quad,
            LifeLength = 0.5f,
            MinVelocity = new Vec3f(-1, 2, -1),
            AddVelocity = new Vec3f(2, 0, 2),
            MinSize = 0.07f,
            MaxSize = 0.1f,
        };

        static RoughCutStorage()
        {
            interactParticles.ParticleModel = EnumParticleModel.Quad;
            interactParticles.AddPos.Set(.5, .5, .5);
            interactParticles.MinQuantity = 5;
            interactParticles.AddQuantity = 20;
            interactParticles.LifeLength = 2.5f;
            interactParticles.MinSize = 0.1f;
            interactParticles.MaxSize = 0.4f;
            interactParticles.MinVelocity.Set(-0.4f, -0.4f, -0.4f);
            interactParticles.AddVelocity.Set(0.8f, 1.2f, 0.8f);
            interactParticles.DieOnRainHeightmap = false;
        }



        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);
            RoughCutStorageBE be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as RoughCutStorageBE;
            if (be == null)
            {
                return true;
            }

            if (!byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.HasAttribute("stonestored"))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.SetInt("stonestored", 1);
            }

            be.istack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Clone();
            byPlayer.InventoryManager.ActiveHotbarSlot.TakeOutWhole();
            return true;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is GenericStorageCapBE)
            {
                be = world.BlockAccessor.GetBlockEntity((be as GenericStorageCapBE).core);
            }
            RoughCutStorageBE rcbe = be as RoughCutStorageBE;

            if (rcbe != null && rcbe.istack.Attributes.GetInt("stonestored") > 0)
            {
                world.SpawnItemEntity(rcbe.istack.Clone(), pos.ToVec3d());
            }
            
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public int dropcount(int chances, int rate)
        {
            int rcount = 0;
            Random r = new Random();
            for (int i = 1; i <= chances; i++)
            {
                if (r.Next(0, 100) <= rate)
                {
                    rcount += 1;
                }
            }
            return rcount;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            AssetLocation interactsound = new AssetLocation("game", "sounds/block/heavyice");
            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is GenericStorageCapBE)
                be = world.BlockAccessor.GetBlockEntity((be as GenericStorageCapBE).core);
            RoughCutStorageBE rcbe = be as RoughCutStorageBE; // casts the block entity as a roughcut block entity.

            if (rcbe == null || rcbe.istack == null)
            {
                return false;
            }

            if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack == null)
            {
                if (rcbe.istack.Attributes.HasAttribute("stonestored"))
                {
                    if (world.Side == EnumAppSide.Client)
                    {
                        (byPlayer as IClientPlayer).ShowChatNotification("Contains: " + rcbe.istack.Attributes["stonestored"].ToString() + " stone");
                    }
                    //rcbe.istack.Attributes.SetInt("stonestored", rcbe.istack.Attributes.GetInt("stonestored") - 1);
                }
                else
                {
                    return false;
                }
            } // if nothing is in the hotbar this gets called.

            else // if something is in the hotbar this gets called.
            {
                ItemStack pistack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;

                AssetLocation dropItemString = null;
                ItemStack dropStack = null;


                if (world.Side == EnumAppSide.Client)
                {
                    (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                }

                if (rcbe.istack.Attributes.GetInt("stonestored") <= 0)
                {
                    world.BlockAccessor.BreakBlock(blockSel.Position, byPlayer);
                    return true;
                }

                if (pistack.ItemAttributes == null)
                {
                    return false;
                }


                interactParticles.ColorByBlock = world.BlockAccessor.GetBlock(blockSel.Position);
                interactParticles.MinPos = blockSel.Position.ToVec3d() + blockSel.HitPosition;
                world.SpawnParticles(interactParticles, byPlayer);
                world.PlaySoundAt(interactsound, byPlayer, byPlayer, true, 32, .5f);
                if (pistack.Attributes.HasAttribute("sstacks"))
                {
                    rcbe.istack.Attributes.SetInt("stonestored", pistack.Attributes.GetInt("sstacks"));
                }


                if (pistack.ItemAttributes.KeyExists("polishedrrate"))
                {
                    dropItemString = new AssetLocation("game", "rockpolished-" + rcbe.istack.Block.FirstCodePart(1));
                    Block tblock = world.GetBlock(dropItemString);

                    if (tblock != null)
                    {
                        dropStack = new ItemStack(world.GetBlock(dropItemString), dropcount(pistack.ItemAttributes["rchances"].AsInt(), pistack.ItemAttributes["polishedrrate"].AsInt()));
                        world.SpawnItemEntity(dropStack, blockSel.Position.ToVec3d() + blockSel.HitPosition, new Vec3d(.05 * blockSel.Face.Normalf.ToVec3d().X, .1, .05 * blockSel.Face.Normalf.ToVec3d().Z));
                        rcbe.istack.Attributes.SetInt("stonestored", rcbe.istack.Attributes.GetInt("stonestored") - 1);
                        if (rcbe.istack.Attributes.GetInt("stonestored") <= 0)
                        {
                            world.BlockAccessor.BreakBlock(blockSel.Position, byPlayer);
                        }
                        byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Item.DamageItem(world, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot, 1);

                    }
                }

                else if (pistack.ItemAttributes.KeyExists("brickrrate"))
                {
                    dropItemString = new AssetLocation("game", "stonebrick-" + rcbe.istack.Block.FirstCodePart(1));
                    Item titem = world.GetItem(dropItemString);

                    if (titem != null)
                    {
                        dropStack = new ItemStack(world.GetItem(dropItemString), dropcount(pistack.ItemAttributes["rchances"].AsInt(), pistack.ItemAttributes["brickrrate"].AsInt()));
                        world.SpawnItemEntity(dropStack, blockSel.Position.ToVec3d() + blockSel.HitPosition, new Vec3d(.05 * blockSel.Face.Normalf.ToVec3d().X, .1, .05 * blockSel.Face.Normalf.ToVec3d().Z));
                        rcbe.istack.Attributes.SetInt("stonestored", rcbe.istack.Attributes.GetInt("stonestored") - 1);
                        if (rcbe.istack.Attributes.GetInt("stonestored") <= 0)
                        {
                            world.BlockAccessor.BreakBlock(blockSel.Position, byPlayer);
                        }
                        byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Item.DamageItem(world, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot, 1);

                    }
                }

                else if (pistack.ItemAttributes.KeyExists("stonerrate"))
                {
                    dropItemString = new AssetLocation("game", "stone-" + rcbe.istack.Block.FirstCodePart(1));
                    Item titem = world.GetItem(dropItemString);

                    if (titem != null)
                    {
                        dropStack = new ItemStack(world.GetItem(dropItemString), dropcount(pistack.ItemAttributes["rchances"].AsInt(), pistack.ItemAttributes["stonerrate"].AsInt()));
                        world.SpawnItemEntity(dropStack, blockSel.Position.ToVec3d()+blockSel.HitPosition, new Vec3d(.05 * blockSel.Face.Normalf.ToVec3d().X, .1, .05 * blockSel.Face.Normalf.ToVec3d().Z));
                        rcbe.istack.Attributes.SetInt("stonestored", rcbe.istack.Attributes.GetInt("stonestored") - 1);
                        if (rcbe.istack.Attributes.GetInt("stonestored") <= 0)
                        {
                            world.BlockAccessor.BreakBlock(blockSel.Position, byPlayer);
                        }
                        byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Item.DamageItem(world, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot, 1);
                    }
                }

                else if (pistack.ItemAttributes.KeyExists("rockrrate"))
                {
                    dropItemString = new AssetLocation("game", "rock-" + rcbe.istack.Block.FirstCodePart(1));
                    Block tblock = world.GetBlock(dropItemString);

                    if (tblock != null)
                    {
                        dropStack = new ItemStack(world.GetBlock(dropItemString), dropcount(pistack.ItemAttributes["rchances"].AsInt(), pistack.ItemAttributes["rockrrate"].AsInt()));
                        world.SpawnItemEntity(dropStack, blockSel.Position.ToVec3d() + blockSel.HitPosition, new Vec3d(.05 * blockSel.Face.Normalf.ToVec3d().X, .1, .05 * blockSel.Face.Normalf.ToVec3d().Z));
                        rcbe.istack.Attributes.SetInt("stonestored", rcbe.istack.Attributes.GetInt("stonestored") - 1);
                        if (rcbe.istack.Attributes.GetInt("stonestored") <= 0)
                        {
                            world.BlockAccessor.BreakBlock(blockSel.Position, byPlayer);
                        }
                        byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Item.DamageItem(world, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot, 1);
                    }
                }
            }

            return true;
        }
    }

    class RoughCutStorageBE : GenericStorageCoreBE
    {
        //used to store the Item used to create this block.
        public ItemStack istack = null;

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            if (tree.HasAttribute("istack"))
            {
                istack = tree.GetItemstack("istack");
                istack.ResolveBlockOrItem(worldAccessForResolve);
            }
            base.FromTreeAttributes(tree, worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            if (istack != null)
            {
                tree.SetItemstack("istack", istack);
            }
            base.ToTreeAttributes(tree);
        }
    }


    class RubbleStorage : GenericStoneStorage
    {
        static SimpleParticleProperties interactParticles = new SimpleParticleProperties()
        {
            MinPos = new Vec3d(),
            AddPos = new Vec3d(),
            MinQuantity = 0,
            AddQuantity = 3,
            GravityEffect = .9f,
            WithTerrainCollision = true,
            ParticleModel = EnumParticleModel.Quad,
            LifeLength = 0.5f,
            MinVelocity = new Vec3f(-1, 2, -1),
            AddVelocity = new Vec3f(2, 0, 2),
            MinSize = 0.07f,
            MaxSize = 0.1f,
        };

        static RubbleStorage()
        {
            interactParticles.ParticleModel = EnumParticleModel.Quad;
            interactParticles.AddPos.Set(.5, .5, .5);
            interactParticles.MinQuantity = 5;
            interactParticles.AddQuantity = 20;
            interactParticles.LifeLength = 2.5f;
            interactParticles.MinSize = 0.1f;
            interactParticles.MaxSize = 0.4f;
            interactParticles.MinVelocity.Set(-0.4f, -0.4f, -0.4f);
            interactParticles.AddVelocity.Set(0.8f, 1.2f, 0.8f);
            interactParticles.DieOnRainHeightmap = false;
        }


        //Adds sand, gravel, and muddy gravel production.
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            dsc.AppendLine("Stone Type: " + inSlot.Itemstack.Attributes.GetString("type"));
            dsc.AppendLine("Stone Amount: " + inSlot.Itemstack.Attributes.GetInt("stone").ToString());
            dsc.AppendLine("Gravel Amount: " + inSlot.Itemstack.Attributes.GetInt("gravel").ToString());
            dsc.AppendLine("Sand Amount: " + inSlot.Itemstack.Attributes.GetInt("sand").ToString());
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            RubbleStorageBE rsbe = world.BlockAccessor.GetBlockEntity(pos) as RubbleStorageBE;

            if (be is GenericStorageCapBE)
            {
                rsbe = world.BlockAccessor.GetBlockEntity((be as GenericStorageCapBE).core) as RubbleStorageBE;
            }
            if (rsbe == null)
            {
                return "";
            }
            string stonelock = "";
            string gravlock = "";
            string sandlock = "";

            switch (rsbe.storageLock)
            {
                case RubbleStorageBE.storageLocksEnum.stone:
                    {
                        stonelock = " : Locked";
                        break;
                    }
                case RubbleStorageBE.storageLocksEnum.gravel:
                    {
                        gravlock = " : Locked";
                        break;
                    }
                case RubbleStorageBE.storageLocksEnum.sand:
                    {
                        sandlock = " : Locked";
                        break;
                    }
                default:
                    break;
            }

            string rstring = "Type: " + rsbe.storedType +
                "\nStone Stored: " + rsbe.storedtypes["stone"].ToString() + stonelock +
                "\nGravel Stored: " + rsbe.storedtypes["gravel"].ToString() + gravlock +
                "\nSand Stored: " + rsbe.storedtypes["sand"].ToString() + sandlock;

            return rstring;
            //return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            int stockMod = 1;
            if (byPlayer.Entity.Controls.Sprint)
            {
                stockMod = byPlayer.InventoryManager.ActiveHotbarSlot.MaxSlotStackSize;
            }

            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            RubbleStorageBE rcbe = world.BlockAccessor.GetBlockEntity(blockSel.Position) as RubbleStorageBE;
            if (be is GenericStorageCapBE)
            {
                rcbe = world.BlockAccessor.GetBlockEntity((be as GenericStorageCapBE).core) as RubbleStorageBE;
            }
            if (rcbe == null)
            {
                return true;
            }

            // if the player is looking at one of the buttons on the crate.
            if (blockSel.SelectionBoxIndex == 1 && byPlayer.Entity.Controls.Sneak)
            {
                setLock(rcbe, RubbleStorageBE.storageLocksEnum.sand);
            }
            else if (blockSel.SelectionBoxIndex == 2 && byPlayer.Entity.Controls.Sneak)
            {
                setLock(rcbe, RubbleStorageBE.storageLocksEnum.gravel);
            }
            else if (blockSel.SelectionBoxIndex == 3 && byPlayer.Entity.Controls.Sneak)
            {
                setLock(rcbe, RubbleStorageBE.storageLocksEnum.stone);
            }

            if (blockSel.SelectionBoxIndex == 1)
            {
                if (rcbe.removeResource(world, byPlayer, blockSel, "sand", stockMod))
                {
                    if (world.Side == EnumAppSide.Client)
                    {
                        (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                    }
                    world.PlaySoundAt(new AssetLocation("game", "sounds/effect/stonecrush"), byPlayer, byPlayer);
                    interactParticles.MinPos = blockSel.Position.ToVec3d() + blockSel.HitPosition;
                    interactParticles.ColorByBlock = world.BlockAccessor.GetBlock(blockSel.Position);
                    world.SpawnParticles(interactParticles, byPlayer);
                    
                }
            }
            else if (blockSel.SelectionBoxIndex == 2)
            {
                if (rcbe.removeResource(world, byPlayer, blockSel, "gravel", stockMod))
                {
                    if (world.Side == EnumAppSide.Client)
                    {
                        (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                    }
                    world.PlaySoundAt(new AssetLocation("game", "sounds/effect/stonecrush"), byPlayer, byPlayer);
                    interactParticles.MinPos = blockSel.Position.ToVec3d() + blockSel.HitPosition;
                    interactParticles.ColorByBlock = world.BlockAccessor.GetBlock(blockSel.Position);
                    world.SpawnParticles(interactParticles, byPlayer);
                }
            }
            else if (blockSel.SelectionBoxIndex == 3)
            {
                if (rcbe.removeResource(world, byPlayer, blockSel, "stone", stockMod))
                {
                    if (world.Side == EnumAppSide.Client)
                    {
                        (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                    }
                    world.PlaySoundAt(new AssetLocation("game", "sounds/effect/stonecrush"), byPlayer, byPlayer);
                    interactParticles.MinPos = blockSel.Position.ToVec3d() + blockSel.HitPosition;
                    interactParticles.ColorByBlock = world.BlockAccessor.GetBlock(blockSel.Position);
                    world.SpawnParticles(interactParticles, byPlayer);
                }
            }

            else if (blockSel.SelectionBoxIndex == 0 && byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack != null)
            {
                // attempts to add the players resource to the block.
                if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.ItemAttributes["rubbleable"].AsBool())
                {
                    if (rcbe.degrade())
                    {
                        if (world.Side == EnumAppSide.Client)
                        {
                            (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                        }
                        world.PlaySoundAt(new AssetLocation("game", "sounds/block/heavyice"), byPlayer, byPlayer);
                    }
                }
                else if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.GetTreeAttribute("contents") != null
                    && byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.GetTreeAttribute("contents").GetItemstack("0") != null)
                {
                    ItemStack tstack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.GetTreeAttribute("contents").GetItemstack("0");
                    if (tstack.Collectible.Code.Domain == "game" && tstack.Collectible.Code.Path == "waterportion")
                    {
                        if (rcbe.drench(world, blockSel))
                        {
                            if (world.Side == EnumAppSide.Client)
                            {
                                (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                            }
                            world.PlaySoundAt(new AssetLocation("game", "sounds/environment/largesplash1"), byPlayer, byPlayer);
                        }
                    }
                }
                else
                {
                    if (rcbe.addResource(byPlayer.InventoryManager.ActiveHotbarSlot, stockMod))
                    {
                        if (world.Side == EnumAppSide.Client)
                        {
                            (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                        }
                        world.PlaySoundAt(new AssetLocation("game", "sounds/effect/stonecrush"), byPlayer, byPlayer);
                    }
                }
            }
            else if (blockSel.SelectionBoxIndex == 0 && byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack == null)
            {
                // if the players hand is empty we want to take all the matching blocks outs of their inventory.
                if (rcbe.addAll(byPlayer))
                {
                    if (world.Side == EnumAppSide.Client)
                    {
                        (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                    }
                    world.PlaySoundAt(new AssetLocation("game", "sounds/effect/stonecrush"), byPlayer, byPlayer);
                }
            }
            rcbe.checkDisplayVariant(world, blockSel);
            
            return true;
        }

        public void setLock(RubbleStorageBE rsbe, RubbleStorageBE.storageLocksEnum toLock)
        {
            rsbe.storageLock = toLock;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is GenericStorageCapBE)
            {
                world.BlockAccessor.BreakBlock((be as GenericStorageCapBE).core, byPlayer);
                return;
            }

            RubbleStorageBE rsbe = be as RubbleStorageBE;

            if (rsbe != null)
            {
                ItemStack dropstack = new ItemStack(world.BlockAccessor.GetBlock(pos));
                dropstack.Attributes.SetString("type", rsbe.storedType);
                dropstack.Attributes.SetInt("stone", rsbe.storedtypes["stone"]);
                dropstack.Attributes.SetInt("gravel", rsbe.storedtypes["gravel"]);
                dropstack.Attributes.SetInt("sand", rsbe.storedtypes["sand"]);
                world.SpawnItemEntity(dropstack, pos.ToVec3d());
            }
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);
            if (byItemStack == null)
            {
                return false;
            }
            RubbleStorageBE rsbe = world.BlockAccessor.GetBlockEntity(blockSel.Position) as RubbleStorageBE;
            rsbe.storedType = byItemStack.Attributes.GetString("type", "");
            rsbe.storedtypes["stone"] = byItemStack.Attributes.GetInt("stone", 0);
            rsbe.storedtypes["gravel"] = byItemStack.Attributes.GetInt("gravel", 0);
            rsbe.storedtypes["sand"] = byItemStack.Attributes.GetInt("sand", 0);
            if (byItemStack.ItemAttributes.KeyExists("maxStorable"))
            {
                rsbe.maxStorable = byItemStack.ItemAttributes["maxStorable"].AsInt();
            }

            return true;
        }
    }

    class RubbleStorageBE : GenericStorageCoreBE
    {
        string[] allowedTypes = {
            "andesite",
            "chalk" ,
            "chert",
            "conglomerate",
            "limestone",
            "claystone",
            "granite",
            "sandstone",
            "shale",
            "basalt",
            "peridotite",
            "phyllite",
            "slate",
            "bauxite"
        };
        public enum storageLocksEnum : int
        {
            none = 0,
            sand = 1,
            gravel = 2,
            stone = 3
        }
        public string storedType = "";
        public storageLocksEnum storageLock = storageLocksEnum.none;
        public string lastAdded = "";
        public int maxStorable = 0;

        public IDictionary<string, int> storedtypes = new Dictionary<string, int>()
        {
            { "sand", 0},
            { "gravel", 0},
            { "stone", 0},
        };        

        public void checkDisplayVariant(IWorldAccessor world, BlockSelection blocksel)
        {
            //Set's the displayed block to the type that has the largest amount of stored material.
            RubbleStorage cblock = world.BlockAccessor.GetBlock(blocksel.Position) as RubbleStorage;


            string bshouldbe = "empty";
            string maxstored = "";
            int maxAmountStored = 0;

            Dictionary<string, string> changeDict;

            foreach (KeyValuePair<string, int> i in storedtypes)
            {
                if (i.Value > maxAmountStored)
                {
                    maxAmountStored = i.Value;
                    maxstored = i.Key;
                }
            }

            if (maxAmountStored > 0)
            {
                bshouldbe = maxstored;

                changeDict = new Dictionary<string, string>()
                { { "type", bshouldbe }, { "stone", storedType} };
            }
            else
            {
                changeDict = new Dictionary<string, string>()
                { { "type", bshouldbe }, { "stone", storedType} };
                
                storedType = "";
            }


            if (bshouldbe != cblock.FirstCodePart(2))
            {
                cblock.SwitchVariant(world, blocksel, changeDict);
            }
        }

        public bool removeResource(IWorldAccessor world, IPlayer byplayer, BlockSelection blockSel, string stype, int quant)
        {
            if (storedType == "" || storedtypes[stype] <= 0)
            {
                return false;
            }
            if (quant > storedtypes[stype])
            {
                quant = storedtypes[stype];
            }

            ItemStack givestack;
            if (stype == "sand" || stype == "gravel")
            {
                Block filler = world.GetBlock(new AssetLocation("game", stype + "-" + storedType));
                givestack = new ItemStack(filler, Math.Min(filler.MaxStackSize, quant));
            }
            else
            {
                Item filler = world.GetItem(new AssetLocation("game", stype + "-" + storedType));
                givestack = new ItemStack(filler, Math.Min(filler.MaxStackSize, quant));
            }

            if (!byplayer.InventoryManager.TryGiveItemstack(givestack.Clone()))
            {
                world.SpawnItemEntity(givestack.Clone(), blockSel.HitPosition+(blockSel.HitPosition.Normalize() * .5)+blockSel.Position.ToVec3d(), blockSel.HitPosition.Normalize()*.05);
            }
            storedtypes[stype] -= givestack.StackSize;
            return true;
        }

        public bool addResource(ItemSlot islot, int quant)
        {
            string btype = "";
            string rtype = "";
            string lstype = "";

            if (storedtypes["stone"] + storedtypes["gravel"] + storedtypes["sand"] + quant > maxStorable)
            {
                quant = maxStorable - (storedtypes["stone"] + storedtypes["gravel"] + storedtypes["sand"]);
                if (quant == 0)
                { return false; }
            }
            if (islot.Itemstack.Item == null)
            {
                rtype = islot.Itemstack.Block.FirstCodePart(1);
                btype = islot.Itemstack.Block.FirstCodePart(0);
                lstype = islot.Itemstack.Block.Code.Path;
            }
            else
            {
                rtype = islot.Itemstack.Item.FirstCodePart(1);
                btype = islot.Itemstack.Item.FirstCodePart(0);
                lstype = islot.Itemstack.Item.Code.Path;
            }

            if (storedType == "" && allowedTypes.Any(rtype.Contains))
            {
                storedType = rtype;
            }
            if (storedType == rtype)
            {
                if (islot.Itemstack.StackSize - quant < 0)
                {
                    quant = islot.Itemstack.StackSize;
                }
                switch (btype)
                {
                    case "sand":
                        {
                            storedtypes["sand"] += quant;
                            break;
                        }
                    case "gravel":
                        {
                            storedtypes["gravel"] += quant;
                            break;
                        }
                    case "stone":
                        {
                            storedtypes["stone"] += quant;
                            break;
                        }
                    default:
                        return false;
                }
                islot.TakeOut(quant);
                lastAdded = lstype;
                return true;
            }
            return false;
        }

        public bool addAll(IPlayer byPlayer)
        {
            // will attempt to add all of a set item type from the players inventory.
            bool psound = false;
            foreach (ItemSlot isl in byPlayer.InventoryManager.GetHotbarInventory())
            {
                if (isl.Itemstack != null && isl.Itemstack.Collectible.Code.Path == lastAdded)
                {
                    if (addResource(isl, isl.StackSize))
                    {
                        psound = true;
                    }
                }
            }
            foreach (KeyValuePair<string, IInventory> inv in byPlayer.InventoryManager.Inventories)
            {
                if (!inv.Key.Contains("creative"))
                {
                    foreach (ItemSlot isl in inv.Value)
                    {
                        if (isl.Itemstack != null && isl.Itemstack.Collectible.Code.Path == lastAdded)
                        {
                            if (addResource(isl, isl.StackSize))
                            {
                                psound = true;
                            }
                        }
                    }
                }
            }

            return psound;
        }

        public bool degrade()
        {
            if (storageLock == storageLocksEnum.stone)
            {
                return false;
            }
            else if (storageLock == storageLocksEnum.gravel)
            {
                if (storedtypes["stone"] > 1)
                {
                    storedtypes["stone"] -= 2;
                    storedtypes["gravel"] += 1;
                    return true;

                }
            }
            else if (storageLock == storageLocksEnum.sand)
            {
                if (storedtypes["gravel"] > 0)
                {
                    storedtypes["gravel"] -= 1;
                    storedtypes["sand"] += 1;
                    return true;

                }
                else if (storedtypes["stone"] > 0)
                {
                    storedtypes["stone"] -= 1;
                    storedtypes["gravel"] += 1;
                    return true;

                }
            }
            else
            {
                if (storedtypes["gravel"] > 0)
                {
                    storedtypes["gravel"] -= 1;
                    storedtypes["sand"] += 1;
                    return true;

                }
                else if (storedtypes["stone"] > 1)
                {
                    storedtypes["stone"] -= 2;
                    storedtypes["gravel"] += 1;
                    return true;
                }
                else
                { return false; }
            }
            return true;
        }

        public bool drench(IWorldAccessor world, BlockSelection blockSel)
        {
            Block dropblock = world.GetBlock(new AssetLocation("game", "muddygravel"));
            if (storedtypes["gravel"] > 0)
            {
                ItemStack dropStack = new ItemStack(dropblock, Math.Min(dropblock.MaxStackSize/4, storedtypes["gravel"]));//Deviding max stack drop by four because not doing so created a mess.
                world.SpawnItemEntity(dropStack.Clone(), blockSel.Position.ToVec3d()+blockSel.HitPosition, (blockSel.Face.Normalf.ToVec3d()*.05)+new Vec3d(0, .08, 0));
                storedtypes["gravel"] -= Math.Min(dropblock.MaxStackSize/4, storedtypes["gravel"]);
                return true;
            }
            return false;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            storedType = tree.GetString("storedType", "");
            storedtypes["sand"] = tree.GetInt("sand", 0);
            storedtypes["gravel"] = tree.GetInt("gravel", 0);
            storedtypes["stone"] = tree.GetInt("stone", 0);
            storageLock = (storageLocksEnum)tree.GetInt("storageLock", 0);
            maxStorable = tree.GetInt("maxStorable");

            base.FromTreeAttributes(tree, worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetString("storedType", storedType);
            tree.SetInt("sand", storedtypes["sand"]);
            tree.SetInt("gravel", storedtypes["gravel"]);
            tree.SetInt("stone", storedtypes["stone"]);
            tree.SetInt("storageLock", (int)storageLock);
            tree.SetInt("maxStorable", maxStorable);
            base.ToTreeAttributes(tree);
        }
    }
}
