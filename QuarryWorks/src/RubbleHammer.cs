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
    class RubbleHammer : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("RubbleHammer", typeof(RubbleHammerTool));
        }
    }

    class RubbleHammerTool : Item
    {
        //A tool for turning stone into gravel and gravel into sand in the world.
        string checkpathr = "rock";
        string checkpathg = "gravel";
        AssetLocation hitsound = new AssetLocation("game", "sounds/block/gravel");

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);

            IWorldAccessor world = byEntity.World;

            if (blockSel != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position) != null)
            {
                Block selectedcodepath = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                if (byEntity.World.BlockAccessor.GetBlock(blockSel.Position).FirstCodePart() == checkpathr)
                {
                    if (world.GetBlock(selectedcodepath.CodeWithPart("gravel", 0)) != null)
                    {
                        world.BlockAccessor.SetBlock(world.GetBlock(selectedcodepath.CodeWithPart("gravel", 0)).Id, blockSel.Position);
                        handling = EnumHandHandling.Handled;
                    }
                }
                else if (byEntity.World.BlockAccessor.GetBlock(blockSel.Position).FirstCodePart() == checkpathg)
                {
                    if (world.GetBlock(selectedcodepath.CodeWithPart("sand", 0)) != null)
                    {
                        world.BlockAccessor.SetBlock(world.GetBlock(selectedcodepath.CodeWithPart("sand", 0)).Id, blockSel.Position);
                        handling = EnumHandHandling.Handled;

                    }
                }
            }
            else
            { return; }

            IPlayer byplayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            world.PlaySoundAt(hitsound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byplayer);
            if (world.Side == EnumAppSide.Client)
            {
                if (byEntity is EntityPlayer)
                {
                    IClientPlayer cbplayer = byplayer as IClientPlayer;
                    if (byplayer != null)
                    {
                        cbplayer.TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                    }
                }
            }
        }
    }
}
