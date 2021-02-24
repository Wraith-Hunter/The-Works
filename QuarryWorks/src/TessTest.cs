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

namespace tessal.src
{
    class ChunksInstantiation : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("tesstestb", typeof(ttestblock));
            api.RegisterBlockEntityClass("tesstest", typeof(ttest));
        }
    }

    class ttestblock : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            (world.BlockAccessor.GetBlockEntity(blockSel.Position) as ttest).increaseh();
            world.BlockAccessor.MarkBlockDirty(blockSel.Position);
            //world.BlockAccessor.MarkBlockEntityDirty(blockSel.Position);
            return true;
        }
    }

    class ttest : BlockEntity
    {
        public float h = 0f;

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            base.OnTesselation(mesher, tessThreadTesselator);
            ICoreClientAPI capi = Api as ICoreClientAPI;
            AssetLocation tasset = new AssetLocation("quarryworks", "shapes/blocks/molds/plugnfeather.json");
            //AssetLocation tasset = new AssetLocation("quarryworks", "shapes/blocks/rubblestorage/RubbleBinClosed.json");
            Shape tshape = capi.Assets.TryGet(tasset).ToObject<Shape>();
            MeshData tmdata = default(MeshData);
            capi.Tesselator.TesselateShape(base.Block, tshape, out tmdata, new Vec3f(0f, 0f, 0f));

            tmdata.Translate(new Vec3f(0f, h, 0f));
            mesher.AddMeshData(tmdata);
            return false;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {           
            base.FromTreeAttributes(tree, worldAccessForResolve);
            h = tree.GetFloat("h");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetFloat("h", h);
            base.ToTreeAttributes(tree);
        }

        public void increaseh()
        {
            h += .1f;
        }
    }
}

   