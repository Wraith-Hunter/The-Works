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
    class ProspectingDrill : Item
    {

        public SimpleParticleProperties useParticles = new SimpleParticleProperties(32, 32, ColorUtil.ColorFromRgba(122, 76, 23, 50), new Vec3d(), new Vec3d(), new Vec3f(), new Vec3f());

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.Handled;
            //base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null)
            {
                return false;
            }
            if (secondsUsed < 3)
            {
                Random rand = new Random();
                useParticles.MinQuantity = 10;
                useParticles.AddQuantity = 10;
                useParticles.MinSize = .3f;
                useParticles.MaxSize = 1f;
                useParticles.MinPos = (blockSel.Position.ToVec3d() + blockSel.HitPosition);
                useParticles.MinVelocity = new Vec3f(-1, 1.3f, -1);
                useParticles.AddVelocity = new Vec3f(2, 1.3f, 2);
                useParticles.ColorByBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                byEntity.World.SpawnParticles(useParticles);
                if (byEntity.World is IClientWorldAccessor)
                {
                    ModelTransform tf = new ModelTransform();
                    tf.EnsureDefaultValues();

                    tf.Origin.Set(0, -1, 0);
                    //tf.Rotation.Y = Math.Min(30, secondsUsed * 40);
                    tf.Rotation.Y = secondsUsed * 160;
                    byEntity.Controls.UsingHeldItemTransformAfter = tf;
                }
                //return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
                return true;
            }
            

            dropSample(byEntity.World, blockSel);
            return false;

        }
        
        

        public void dropSample(IWorldAccessor world, BlockSelection blockSel)
        {
            //drops core sample when 5 seconds are up.
            string iDrop = "coresample";
            Dictionary<string, Dictionary<string, int>> rockTypes = FindRockTypes(world, blockSel.Position);
            string largestType = FindLargestCollection(rockTypes);

            AssetLocation dropItemLocation = new AssetLocation(Code.Domain, iDrop + "-" + largestType);
            ItemStack dropStack = new ItemStack(world.GetItem(dropItemLocation), 1);

            BlockPos tmpBPos = blockSel.Position.Copy();
            tmpBPos.X = tmpBPos.X - world.BlockAccessor.MapSizeX / 2;
            tmpBPos.Z = tmpBPos.Z - world.BlockAccessor.MapSizeZ / 2;

            AttributeSetter(ref dropStack, rockTypes, tmpBPos);
            world.SpawnItemEntity(dropStack, blockSel.Position.ToVec3d()+blockSel.HitPosition, new Vec3d(0, .1, 0));
        }
        public Dictionary<string, Dictionary<string, int>> FindRockTypes(IWorldAccessor world, BlockPos pos) // Finds rocks and ores.
        {
            Dictionary<string, Dictionary<string, int>> rdict = new Dictionary<string, Dictionary<string, int>>();
            Dictionary<string, int> rocks = new Dictionary<string, int>();
            Dictionary<string, int> ores = new Dictionary<string, int>();
            BlockPos searchpos = pos.Copy();

            for (int i = 0; i <= pos.Y; i++)
            {
                searchpos.Y = i;
                Block tempblock = world.BlockAccessor.GetBlock(searchpos);
                string tmp = tempblock.FirstCodePart();
                if (tempblock.Code.Domain == "game" && tempblock.FirstCodePart() == "rock")
                {
                    if (rocks.ContainsKey(tempblock.FirstCodePart(1)))
                    {
                        rocks[tempblock.FirstCodePart(1)] += 1;
                    }
                    else
                    {
                        rocks.Add(tempblock.FirstCodePart(1), 1);
                    }
                }
                if (tempblock.Code.Domain == "game" && tempblock.FirstCodePart() == "ore")
                {
                    if (ores.ContainsKey(tempblock.GetPlacedBlockName(world, pos)))
                    {
                        ores[tempblock.GetPlacedBlockName(world, pos)] += 1;
                    }
                    else
                    {
                        ores.Add(tempblock.GetPlacedBlockName(world, pos), 1);
                    }
                }
            }
            rdict.Add("rocks", rocks);
            rdict.Add("ores", ores);
            return rdict;
        }

        public string FindLargestCollection(Dictionary<string, Dictionary<string, int>> rockDict)
        {
            KeyValuePair<string, int> largest = rockDict["rocks"].First();
            foreach (KeyValuePair<string, int> entry in rockDict["rocks"])
            {
                if (entry.Value > largest.Value)
                {
                    largest = entry;
                }
            }
            return largest.Key;
        }

        public Dictionary<string, int> FindAsPercentage(Dictionary<string, int> rockDict)
        {
            Dictionary<string, int> rdict = new Dictionary<string, int>();

            int total = 0;
            foreach (KeyValuePair<string, int> entry in rockDict)
            {
                total += entry.Value;
            }
            foreach (KeyValuePair<string, int> entry in rockDict)
            {
                float tmpVal = (entry.Value * 100 / total);
                rdict.Add(entry.Key, (int)tmpVal);
            }

            return rdict;
        }

        public void AttributeSetter(ref ItemStack iStack, Dictionary<string, Dictionary<string, int>> rockTypes, BlockPos pos)
        {
            int tmpCounter = 0;
            iStack.Attributes.SetInt("rcount", rockTypes["rocks"].Count); //rock count.
            iStack.Attributes.SetInt("ocount", rockTypes["ores"].Count); //ore count.
            iStack.Attributes.SetString("pos", pos.ToString());

            Dictionary<string, int> rockTypesPercentage = FindAsPercentage(rockTypes["rocks"]);
            foreach (KeyValuePair<string, int> entry in rockTypesPercentage)
            {
                iStack.Attributes.SetString("rockName" + tmpCounter, entry.Key);
                iStack.Attributes.SetInt("rockAmount" + tmpCounter, entry.Value);
                tmpCounter += 1;
            }

            tmpCounter = 0;
            foreach (KeyValuePair<string, int> entry in rockTypes["ores"])
            {
                iStack.Attributes.SetString("oreName" + tmpCounter, entry.Key);
                iStack.Attributes.SetInt("oreAmount" + tmpCounter, entry.Value);
                tmpCounter += 1;
            }
        }
    }

    class CoreSample : Item
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            Dictionary<string, int> resolvedDict = ResolveData(inSlot.Itemstack);
            string collectedFrom = ResolvePos(inSlot.Itemstack);
            string ores = ResolveOres(inSlot.Itemstack);

            dsc.AppendLine("Collected From: " + collectedFrom);
            dsc.AppendLine("Traces: "+ ores);


            foreach (KeyValuePair<string, int> entry in resolvedDict)
            {
                string tstring = entry.Key;
                tstring = FirstCharToUpper(tstring);
                dsc.AppendLine(tstring + ": " + entry.Value.ToString().ToUpperInvariant() + "%");
            }
        }

        public static string FirstCharToUpper(string input)
        {
            //taken directly from stackoverflow. https://stackoverflow.com/questions/4135317/make-first-letter-of-a-string-upper-case-with-maximum-performance
            if (String.IsNullOrEmpty(input))
                throw new ArgumentException("ARGH!");
            return input.First().ToString().ToUpper() + input.Substring(1);
        }


        public string ResolvePos(ItemStack iStack)
        {
            string rString = "";

            rString = iStack.Attributes.GetString("pos", "");

            return rString;
        }

        public string ResolveOres(ItemStack iStack)
        {
            string rstring = "";
            int? tempcount = iStack.Attributes.TryGetInt("ocount");

            if (tempcount != null && tempcount != 0)
            {
                rstring = iStack.Attributes.GetString("oreName0");
                for (int i = 1; i < tempcount; i++)
                {
                    rstring = rstring + ", " + iStack.Attributes.GetString("oreName" + i);
                }
            }

            if (rstring == "")
            {
                rstring = "No ore traces found";
            }

            return rstring;
        }

        public Dictionary<string, int> ResolveData(ItemStack iStack)
        //turns a stored items stacks attributes into a dict.
        {
            Dictionary<string, int> rdict = new Dictionary<string, int>();
            int? tempcount = iStack.Attributes.TryGetInt("rcount");
            if (tempcount != null && tempcount != 0 )
            {
                for (int i = 0; i < tempcount; i++)
                {
                    rdict.Add(iStack.Attributes.GetString("rockName" + i), iStack.Attributes.GetInt("rockAmount"+i));
                }
            }
            return rdict;
        }



    }
}
