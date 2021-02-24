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
    class ModConfigFile
    {
        public static ModConfigFile Current { get; set; }
        public int test = 1;
        
    }

    class QuarryWorks: ModSystem
    {

        public override void StartPre(ICoreAPI api)
        {
            Debug.WriteLine("starting pre");
            ModConfigFile.Current = api.LoadModConfig<ModConfigFile>("test/test!.json");
            if (ModConfigFile.Current == null)
            {
                api.StoreModConfig(new ModConfigFile(), "test/test!.json");
                Debug.WriteLine("file not found.");
            }
            base.StartPre(api);
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterBlockClass("PlugFeatherBlock", typeof(PlugnFeatherBlock));
            api.RegisterBlockEntityClass("PlugFeatherBE", typeof(PlugnFeatherBlockEntity));

            api.RegisterBlockClass("RubbleStorage", typeof(RubbleStorage));
            api.RegisterBlockEntityClass("RubbleStorageBE", typeof(RubbleStorageBE));

            api.RegisterBlockClass("RoughStoneStorage", typeof(RoughCutStorage));
            api.RegisterBlockEntityClass("StoneStorageCoreBE", typeof(RoughCutStorageBE));
            api.RegisterBlockEntityClass("StoneStorageCapBE", typeof(GenericStorageCapBE));

            api.RegisterItemClass("ProspectingDrill", typeof(ProspectingDrill));
            api.RegisterItemClass("CoreSample", typeof(CoreSample));
        }
    }
}
