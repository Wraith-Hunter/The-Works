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

            if (ablock.Attributes != null && ablock.Attributes.KeyExists("caps"))
            {
                for (int i = 0; i < ablock.Attributes["caps"].AsArray().Length; i++)
                {
                    Dictionary<string, string> rdict = new Dictionary<string, string>();

                    foreach (JsonObject obj in ablock.Attributes["caps"].AsArray()[i]["varType"].AsArray())
                    {
                        Debug.WriteLine(obj.AsArray()[0]);
                        Debug.WriteLine(obj.AsArray()[1]);
                        rdict.Add(obj.AsArray()[0].AsString(), obj.AsArray()[1].AsString());
                    }  

                    //Block capBlock = world.GetBlock(CodeWithVariant("dir", ablock.Attributes["caps"].AsArray()[0]["var"].AsString()));
                    Block capBlock = world.GetBlock(CodeWithVariants(rdict));
                    BlockPos capPos = blockSel.Position.Copy() + new BlockPos(ablock.Attributes["caps"].AsArray()[i]["x"].AsInt(), ablock.Attributes["caps"].AsArray()[i]["y"].AsInt(), ablock.Attributes["caps"].AsArray()[i]["z"].AsInt());
                    world.BlockAccessor.ExchangeBlock(capBlock.Id, capPos);

                    world.BlockAccessor.SpawnBlockEntity("StoneStorageCapBE", capPos);
                    (world.BlockAccessor.GetBlockEntity(capPos) as GenericStorageCapBE).core = blockSel.Position;
                    (world.BlockAccessor.GetBlockEntity(blockSel.Position) as GenericStorageCoreBE).caps.Add(capPos);
                }
            }
            return true;
        }
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            BlockPos masterpos = null;
            if (be == null)
            {
                world.BlockAccessor.SetBlock(0, pos);
                return;
            }

            if (be is GenericStorageCapBE)
            {
                masterpos = (be as GenericStorageCapBE).core;
                be = world.BlockAccessor.GetBlockEntity((be as GenericStorageCapBE).core);
            }
            else
            {
                masterpos = pos;
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
            if (!base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack)) { return false; };
            RoughCutStorageBE be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as RoughCutStorageBE;
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
                        (byPlayer as IClientPlayer).ShowChatNotification("Contains: "+rcbe.istack.Attributes["stonestored"].ToString() + " stone");
                    }
                    //rcbe.istack.Attributes.SetInt("stonestored", rcbe.istack.Attributes.GetInt("stonestored") - 1);
                }
                else
                {
                    return false;
                }
            }

            else
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
                    return false;
                }

                interactParticles.ColorByBlock = world.BlockAccessor.GetBlock(blockSel.Position);
                interactParticles.MinPos = blockSel.Position.ToVec3d()+blockSel.HitPosition;
                world.SpawnParticles(interactParticles, byPlayer);

                world.PlaySoundAt(interactsound, byPlayer, byPlayer, true, 32, .5f);
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
}
