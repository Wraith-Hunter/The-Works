using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;

namespace stoneworks.src
{
    class ChunksInstantiation : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("slabsetter", typeof(blockstacksetter));
        }
    }

    class blockstacksetter : Item
    {
        int stonequant = 0;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            IClientPlayer spry = byPlayer as IClientPlayer;

            IWorldAccessor world = byEntity.World;
            IBlockAccessor bacc = world.BlockAccessor;

            Debug.WriteLine(blockSel);
            if (blockSel == null)
            {
                if (byPlayer.Entity.Controls.Sneak && !byPlayer.Entity.Controls.Sprint)
                {
                    stonequant += 10;
                }
                else if (!byPlayer.Entity.Controls.Sprint)
                {
                    stonequant += 1;
                }
                if (byPlayer.Entity.Controls.Sprint && byPlayer.Entity.Controls.Sneak)
                {
                    stonequant -= 10;
                }
                else if (byPlayer.Entity.Controls.Sprint && !byPlayer.Entity.Controls.Sneak)
                {
                    stonequant -= 1;
                }
            }

            slot.Itemstack.Attributes.SetInt("sstacks", stonequant);
            handling = EnumHandHandling.Handled;
            if (spry == null)
            {
                Debug.WriteLine("spry is null");
                return;
            }
            spry.ShowChatNotification(stonequant.ToString());

        }
        
    }

}
