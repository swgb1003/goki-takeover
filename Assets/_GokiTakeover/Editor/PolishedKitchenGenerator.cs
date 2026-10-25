using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using P = GokiTakeover.Editor.PolishedAssets;

namespace GokiTakeover.Editor
{
    public static class PolishedKitchenGenerator
    {
        public const string ScenePath = GeneratedAssets.Root + "/Scenes/Phase1_Kitchen_Polished.unity";
        private static Material walnut, oak, enamel, ivory, metal, black, cloth, amber, leaf, soil, shadow;
        private static Mesh cupMesh, bottleMesh, shadeMesh;

        [MenuItem("Tools/GOKI TAKEOVER/Generate Cinematic Kitchen", priority = 15)]
        public static void GenerateMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
                else Generate();
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        [MenuItem("Tools/GOKI TAKEOVER/Rebuild Cinematic Kitchen", priority = 16)]
        public static void RebuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try { Generate(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        [MenuItem("Tools/GOKI TAKEOVER/Generate Cinematic Kitchen", true)]
        [MenuItem("Tools/GOKI TAKEOVER/Rebuild Cinematic Kitchen", true)]
        private static bool CanGenerate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static void Generate()
        {
            GeneratedAssets.RequireOwned(ScenePath);
            P.Prepare();
            GameObject prefab = PolishedCockroachGenerator.Generate();
            walnut = P.Material("Walnut", new Color(0.52f, 0.39f, 0.25f), 0.32f, 0, P.WoodTexture, new Vector2(1, 3));
            oak = P.Material("WarmOak", new Color(0.80f, 0.68f, 0.49f), 0.25f, 0, P.WoodTexture, new Vector2(1, 2));
            enamel = P.Material("DeepTealEnamel", new Color(0.105f, 0.22f, 0.205f), 0.38f, 0.06f);
            ivory = P.Material("WarmCeramic", new Color(0.68f, 0.65f, 0.53f), 0.48f);
            metal = P.Material("BrushedBrass", new Color(0.51f, 0.38f, 0.19f), 0.60f, 0.68f);
            black = P.Material("Charcoal", new Color(0.025f, 0.032f, 0.032f), 0.28f);
            cloth = P.Material("CheckedLinen", Color.white, 0.08f, 0, P.ClothTexture);
            amber = P.Material("AmberGlassOpaque", new Color(0.28f, 0.105f, 0.024f), 0.72f, 0.1f);
            leaf = P.Material("HerbLeaves", new Color(0.16f, 0.26f, 0.08f), 0.1f);
            soil = P.Material("Terracotta", new Color(0.32f, 0.105f, 0.045f), 0.18f);
            shadow = P.ShadowMaterial("FurnitureContact", 0.48f);
            cupMesh = P.Lathe("HollowCup", new[] { new Vector2(0,-0.5f), new Vector2(0.35f,-0.5f), new Vector2(0.45f,-0.4f),
                new Vector2(0.49f,0.43f), new Vector2(0.46f,0.5f), new Vector2(0.39f,0.46f), new Vector2(0.37f,-0.28f), new Vector2(0,-0.28f) });
            bottleMesh = P.Lathe("BottleProfile", new[] { new Vector2(0,0), new Vector2(0.19f,0), new Vector2(0.22f,0.08f),
                new Vector2(0.22f,0.65f), new Vector2(0.11f,0.84f), new Vector2(0.09f,1.15f), new Vector2(0.11f,1.18f), new Vector2(0,1.18f) }, 16);
            shadeMesh = P.Lathe("PendantShade", new[] { new Vector2(0.68f,0), new Vector2(0.68f,0.07f), new Vector2(0.58f,0.18f),
                new Vector2(0.16f,0.56f), new Vector2(0.10f,0.57f), new Vector2(0.07f,0.50f), new Vector2(0.52f,0.12f), new Vector2(0.63f,0) });

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform root = new GameObject("GOKI_CinematicKitchen").transform;
            Transform room = GeneratedAssets.Empty("Room", root, Vector3.zero);
            Material floor = P.Material("WornStone", Color.white, 0.32f, 0.045f, P.FloorTexture, new Vector2(4.5f, 4));
            P.Box("Floor", room, new Vector3(0,-0.15f,0), new Vector3(14,0.3f,12), floor);
            Material plaster = P.Material("MidnightPlaster", new Color(0.25f,0.31f,0.32f), 0.10f);
            P.Box("Wall_Back", room, new Vector3(0,2.7f,6.12f), new Vector3(14.4f,5.4f,0.24f), plaster);
            P.Box("Wall_Front", room, new Vector3(0,2.7f,-6.12f), new Vector3(14.4f,5.4f,0.24f), plaster);
            P.Box("Wall_Left", room, new Vector3(-7.12f,2.7f,0), new Vector3(0.24f,5.4f,12), plaster);
            P.Box("Wall_Right", room, new Vector3(7.12f,2.7f,0), new Vector3(0.24f,5.4f,12), plaster);
            for (int side = -1; side <= 1; side += 2)
            {
                P.Box("Skirting", room, new Vector3(0,0.15f,side*5.96f), new Vector3(14,0.3f,0.08f), walnut, false);
                P.Box("Skirting", room, new Vector3(side*6.96f,0.15f,0), new Vector3(0.08f,0.3f,12), walnut, false);
            }
            Counter(room);
            Refrigerator(room);
            TableAndChairs(room);
            Shelf(room);
            Props(room);
            Lighting(root);
            foreach (Transform part in room.GetComponentsInChildren<Transform>()) part.gameObject.isStatic = true;

            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.SetPositionAndRotation(new Vector3(-0.2f,0.04f,-4.2f), Quaternion.Euler(0,-12,0));
            var camera = new GameObject("GOKI_CinematicCamera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 55;
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = 35;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f,0.045f,0.06f);
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.gameObject.AddComponent<AudioListener>();
            var follow = camera.gameObject.AddComponent<ThirdPersonRoachCamera>();
            follow.Configure(player.transform);
            follow.ConfigureFraming(2.20f, new Vector3(0,0.58f,0), 6f);
            Quaternion rotation = Quaternion.Euler(6,-12,0);
            camera.transform.SetPositionAndRotation(player.transform.position + Vector3.up*0.58f + rotation*Vector3.back*2.20f, rotation);
            var motor = player.GetComponent<RoachMotor>();
            motor.Configure(camera.transform, player.GetComponentInChildren<CockroachVisualController>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(motor);
            root.gameObject.AddComponent<PolishedPresentation>();
            root.gameObject.AddComponent<PolishedHud>().Configure(motor);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("[GOKI] 改善版シーン保存に失敗しました。");
            GeneratedAssets.Mark(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            AssetDatabase.SaveAssets();
            Debug.Log("[GOKI] 改善版キッチンを生成しました。元の試験シーンとPrefabは変更していません。");
        }

        private static void Counter(Transform parent)
        {
            Transform counter = GeneratedAssets.Empty("KitchenCabinet", parent, new Vector3(1.35f,0,4.8f));
            P.Box("ToeKick", counter, new Vector3(0,0.16f,0.1f), new Vector3(6,0.32f,1.9f), black);
            P.Box("CabinetBody", counter, new Vector3(0,1.45f,0), new Vector3(6,2.45f,2.05f), enamel);
            P.Box("StoneWorktop", counter, new Vector3(0,2.77f,0), new Vector3(6.18f,0.19f,2.24f), ivory);
            for (int i = 0; i < 4; i++)
            {
                float x = -2.25f+i*1.5f;
                P.Box("FramedDoor", counter, new Vector3(x,1.42f,-1.055f), new Vector3(1.41f,2.12f,0.11f), enamel, false);
                P.Box("InsetPanel", counter, new Vector3(x,1.42f,-1.12f), new Vector3(1.17f,1.84f,0.035f), enamel, false);
                P.Rod("BrassHandle", counter, new Vector3(x+0.45f,1.72f,-1.20f), new Vector3(x+0.45f,2.05f,-1.20f),0.046f,metal);
            }
            // A framed window and small backsplash provide depth without a second room.
            P.Box("WindowRecess", parent, new Vector3(0.9f,4.1f,5.94f), new Vector3(3.2f,2,0.12f), black, false);
            var glass = P.Material("NightWindow", new Color(0.055f,0.12f,0.17f),0.55f,0.1f,null,null,new Color(0.055f,0.105f,0.16f));
            P.Box("NightGlass", parent, new Vector3(0.9f,4.1f,5.86f), new Vector3(3.0f,1.8f,0.035f),glass,false);
            P.Box("WindowMullion", parent, new Vector3(0.9f,4.1f,5.81f),new Vector3(0.08f,1.9f,0.08f),ivory,false);
            P.Box("WindowCrossbar",parent,new Vector3(0.9f,4.15f,5.80f),new Vector3(3.1f,0.07f,0.08f),ivory,false);
            P.Box("Sill",parent,new Vector3(0.9f,3.16f,5.65f),new Vector3(3.4f,0.14f,0.50f),oak,false);
            for(int i=0;i<12;i++) P.Box("BacksplashTile",counter,new Vector3(-2.75f+i*0.5f,3.1f,1.03f),new Vector3(0.485f,0.43f,0.025f),ivory,false);
            P.Box("SinkRim",counter,new Vector3(-1.2f,2.89f,0),new Vector3(1.65f,0.055f,1.35f),metal,false);
            P.Box("DarkSink",counter,new Vector3(-1.2f,2.92f,0),new Vector3(1.43f,0.025f,1.13f),black,false);
            P.Rod("TapStem",counter,new Vector3(-1.2f,2.9f,0.65f),new Vector3(-1.2f,3.5f,0.65f),0.07f,metal);
            P.Rod("TapSpout",counter,new Vector3(-1.2f,3.5f,0.65f),new Vector3(-1.2f,3.5f,0.28f),0.07f,metal);
            P.Box("Hob",counter,new Vector3(1.65f,2.9f,0),new Vector3(1.9f,0.075f,1.5f),black,false);
            for(int i=0;i<2;i++)
            {
                P.Sphere("Burner",counter,new Vector3(1.18f+i*0.95f,2.95f,0),new Vector3(0.6f,0.045f,0.6f),metal);
                P.Sphere("BurnerCap",counter,new Vector3(1.18f+i*0.95f,2.98f,0),new Vector3(0.48f,0.035f,0.48f),black);
            }
            Plant(parent,new Vector3(2.12f,3.25f,5.51f),0.45f);
            Bottle(counter,new Vector3(-2.6f,2.88f,0.45f),0.60f);
            P.Box("TeaTowel",counter,new Vector3(-2.1f,2.25f,-1.16f),new Vector3(0.6f,1.05f,0.025f),cloth,false);
        }

        private static void Refrigerator(Transform parent)
        {
            Transform fridge = GeneratedAssets.Empty("OpenRefrigerator",parent,new Vector3(-4.8f,0,4.65f));
            P.Box("Back",fridge,new Vector3(0,2.3f,0.91f),new Vector3(2.3f,4.3f,0.18f),ivory);
            for(int s=-1;s<=1;s+=2) P.Box("Side",fridge,new Vector3(s*1.07f,2.3f,0),new Vector3(0.18f,4.3f,2.0f),ivory);
            P.Box("Top",fridge,new Vector3(0,4.4f,0),new Vector3(2.3f,0.18f,2),ivory);
            P.Box("Base",fridge,new Vector3(0,0.22f,0),new Vector3(2.3f,0.3f,2),black);
            P.Box("FreezerDoor",fridge,new Vector3(0,3.85f,-0.93f),new Vector3(2.1f,1.05f,0.15f),ivory);
            for(int i=0;i<3;i++)
            {
                P.Box("InteriorShelf",fridge,new Vector3(0,0.8f+i*0.82f,0),new Vector3(1.94f,0.065f,1.78f),ivory,false);
                Bottle(fridge,new Vector3(-0.48f,0.85f+i*0.82f,0.2f),0.48f);
                P.Part("Jar",fridge,cupMesh,ivory,new Vector3(0.40f,1.05f+i*0.82f,0.18f),Vector3.one*0.40f);
            }
            var interior = P.Material("FridgeGlow",new Color(0.94f,0.70f,0.36f),0.2f,0,null,null,new Color(0.9f,0.48f,0.13f));
            P.Box("InteriorLight",fridge,new Vector3(0,3.2f,0.76f),new Vector3(1.8f,0.2f,0.05f),interior,false);
            Transform hinge=GeneratedAssets.Empty("DoorHinge",fridge,new Vector3(-1.1f,0,-1.0f));
            hinge.localRotation=Quaternion.Euler(0,58,0);
            P.Box("OpenDoor",hinge,new Vector3(1.1f,1.85f,0),new Vector3(2.16f,2.95f,0.18f),ivory);
            P.Rod("DoorHandle",hinge,new Vector3(1.94f,2.25f,-0.14f),new Vector3(1.94f,2.87f,-0.14f),0.065f,metal);
            P.Box("PaperNote",hinge,new Vector3(0.95f,2.6f,-0.105f),new Vector3(0.46f,0.53f,0.012f),cloth,false);
            P.Shadow("FridgeAO",parent,new Vector3(-4.8f,0.007f,4.2f),new Vector2(3.3f,3.4f),shadow);
        }

        private static void TableAndChairs(Transform parent)
        {
            Transform table=GeneratedAssets.Empty("WalnutDiningTable",parent,new Vector3(-2.25f,0,-0.9f));
            P.Box("Top",table,new Vector3(0,2.7f,0),new Vector3(3.8f,0.20f,2.7f),walnut);
            for(int x=-1;x<=1;x+=2)
            for(int z=-1;z<=1;z+=2)
            {
                Vector3 at=new Vector3(x*1.55f,0,z*1.0f);
                P.Rod("TaperedLeg",table,at+Vector3.up*0.04f,at+Vector3.up*2.62f,0.23f,walnut);
                P.Box("LegCollider",table,at+Vector3.up*1.30f,new Vector3(0.18f,2.6f,0.18f),walnut).GetComponent<MeshRenderer>().enabled=false;
                P.Rod("BrassFoot",table,at+Vector3.up*0.03f,at+Vector3.up*0.16f,0.235f,metal);
                P.Shadow("LegContact",table,at+Vector3.up*0.007f,new Vector2(0.7f,0.7f),shadow);
            }
            P.Box("ApronFront",table,new Vector3(0,2.46f,-1.06f),new Vector3(3.2f,0.35f,0.11f),walnut,false);
            P.Box("ApronSide",table,new Vector3(1.58f,2.46f,0),new Vector3(0.11f,0.35f,2.1f),walnut,false);
            P.Box("LinenRunner",table,new Vector3(0,2.81f,0),new Vector3(1.1f,0.025f,2.65f),cloth,false);
            P.Box("DrapedLinen",table,new Vector3(0,2.31f,-1.35f),new Vector3(1.1f,1.0f,0.025f),cloth,false);
            P.Sphere("Plate",table,new Vector3(0.98f,2.84f,0.2f),new Vector3(0.9f,0.08f,0.9f),ivory);
            Cup(table,new Vector3(-0.95f,3.12f,0.2f),0.58f);
            P.Shadow("TableAO",parent,new Vector3(-2.25f,0.006f,-0.9f),new Vector2(4.8f,3.7f),shadow);
            Chair(parent,new Vector3(-2.25f,0,-3.2f),0);
            Chair(parent,new Vector3(-4.85f,0,-0.9f),90);
        }
        private static void Chair(Transform parent,Vector3 position,float yaw)
        {
            Transform chair=GeneratedAssets.Empty("DiningChair",parent,position);
            chair.localRotation=Quaternion.Euler(0,yaw,0);
            P.Box("Seat",chair,new Vector3(0,1.55f,0),new Vector3(1.3f,0.17f,1.3f),oak);
            for(int x=-1;x<=1;x+=2)
            for(int z=-1;z<=1;z+=2)
            {
                P.Rod("ChairLeg",chair,new Vector3(x*0.53f,0,z*0.53f),new Vector3(x*0.48f,1.52f,z*0.48f),0.13f,walnut);
                P.Box("LegCollider",chair,new Vector3(x*0.5f,0.75f,z*0.5f),new Vector3(0.13f,1.5f,0.13f),walnut).GetComponent<MeshRenderer>().enabled=false;
            }
            for(int x=-1;x<=1;x+=2) P.Rod("BackPost",chair,new Vector3(x*0.53f,1.5f,-0.53f),new Vector3(x*0.53f,2.9f,-0.67f),0.13f,walnut);
            for(int i=0;i<3;i++) P.Box("BackSlat",chair,new Vector3((i-1)*0.34f,2.25f,-0.60f),new Vector3(0.17f,1.1f,0.075f),oak,false);
            P.Box("BackRail",chair,new Vector3(0,2.9f,-0.67f),new Vector3(1.3f,0.20f,0.16f),walnut);
            P.Shadow("ChairAO",chair,new Vector3(0,0.006f,0),new Vector2(2,2),shadow);
        }
        private static void Shelf(Transform parent)
        {
            Transform shelf=GeneratedAssets.Empty("PantryShelf",parent,new Vector3(5.85f,0,2.2f));
            for(int i=0;i<4;i++)
            {
                P.Box("ShelfBoard",shelf,new Vector3(0,0.45f+i*1.15f,0),new Vector3(1.5f,0.12f,3.2f),oak);
                for(int j=0;j<3;j++)
                {
                    if((i+j)%2==0) Bottle(shelf,new Vector3(0,0.52f+i*1.15f,(j-1)*0.95f),0.65f);
                    else P.Part("CeramicJar",shelf,cupMesh,ivory,new Vector3(0,0.80f+i*1.15f,(j-1)*0.95f),new Vector3(0.62f,0.55f,0.62f));
                }
            }
            for(int x=-1;x<=1;x+=2)
            for(int z=-1;z<=1;z+=2) P.Box("ShelfPost",shelf,new Vector3(x*0.67f,2,z*1.45f),new Vector3(0.10f,4,0.10f),black);
            P.Shadow("ShelfAO",parent,new Vector3(5.8f,0.007f,2.2f),new Vector2(2.5f,4.0f),shadow);
        }
        private static void Props(Transform parent)
        {
            var bread=P.Material("Breadcrumb",new Color(0.66f,0.39f,0.13f),0.08f);
            Vector3[] locations={new Vector3(-0.5f,0.07f,1.5f),new Vector3(3.6f,0.07f,3.2f),new Vector3(-4.5f,0.07f,-3.8f)};
            for(int i=0;i<locations.Length;i++)
            {
                Transform food=GeneratedAssets.Empty("FoodMarker_"+i,parent,locations[i]);
                for(int j=0;j<6;j++)
                {
                    Transform crumb=P.Box("Crumb",food,new Vector3(Mathf.Sin(j*3)*0.24f,j%2*0.04f,Mathf.Cos(j*4)*0.19f),Vector3.one*(0.10f+j%3*0.035f),bread,false);
                    crumb.localRotation=Quaternion.Euler(j*17,j*47,j*11);
                }
                P.Shadow("CrumbAO",food,new Vector3(0,-0.063f,0),new Vector2(0.85f,0.7f),shadow);
            }
            // A single static slipper marker replaces the prototype block mannequin; it has no AI.
            Transform humanMarker=GeneratedAssets.Empty("HumanMarker_StaticSlipper",parent,new Vector3(3.6f,0,-1.5f));
            P.Sphere("SlipperSole",humanMarker,new Vector3(0,0.09f,0),new Vector3(0.8f,0.17f,1.65f),black);
            P.Sphere("SlipperUpper",humanMarker,new Vector3(0,0.23f,0.28f),new Vector3(0.77f,0.42f,0.95f),enamel);
            P.Box("SlipperCollision",humanMarker,new Vector3(0,0.17f,0),new Vector3(0.8f,0.34f,1.6f),black).GetComponent<MeshRenderer>().enabled=false;
            P.Shadow("SlipperAO",humanMarker,new Vector3(0,0.006f,0),new Vector2(1.3f,2.1f),shadow);
            Plant(parent,new Vector3(5.85f,4.05f,3.0f),0.75f);
            Bottle(parent,new Vector3(1.65f,0.012f,-2.5f),0.8f,true);
            Transform fallenCup=P.Part("FallenCup",parent,cupMesh,ivory,new Vector3(1.8f,0.29f,-3.1f),Vector3.one*0.58f);
            fallenCup.localRotation=Quaternion.Euler(0,-22,90);
            var cupCollider=fallenCup.gameObject.AddComponent<CapsuleCollider>();
            cupCollider.radius=0.49f;
            cupCollider.height=1.0f;
            P.Shadow("CupAO",parent,new Vector3(1.8f,0.007f,-3.1f),new Vector2(1.1f,0.9f),shadow);
        }
        private static void Lighting(Transform root)
        {
            RenderSettings.skybox=null;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(0.16f,0.21f,0.30f);
            RenderSettings.ambientEquatorColor=new Color(0.14f,0.13f,0.12f);
            RenderSettings.ambientGroundColor=new Color(0.075f,0.060f,0.045f);
            RenderSettings.reflectionIntensity=0.28f;
            RenderSettings.fog=true;
            RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=new Color(0.07f,0.105f,0.13f);
            RenderSettings.fogDensity=0.022f;
            var key=new GameObject("WindowKey_OneShadowMap").AddComponent<Light>();
            key.transform.SetParent(root);
            key.type=LightType.Directional;
            key.color=new Color(1f,0.79f,0.52f);
            key.intensity=1.25f;
            key.shadows=LightShadows.Soft;
            key.shadowStrength=0.65f;
            key.shadowBias=0.025f;
            key.shadowNormalBias=0.18f;
            key.transform.rotation=Quaternion.Euler(43,-28,0);
            Transform pendant=GeneratedAssets.Empty("Pendant",root,new Vector3(-1.5f,4.7f,0.5f));
            P.Part("Shade",pendant,shadeMesh,enamel,Vector3.zero,Vector3.one);
            P.Rod("Cable",pendant,new Vector3(0,0.55f,0),new Vector3(0,1.5f,0),0.025f,black);
            var glow=P.Material("LampGlow",new Color(1f,0.7f,0.3f),0.2f,0,null,null,new Color(1.5f,0.80f,0.25f));
            P.Sphere("Bulb",pendant,new Vector3(0,0.10f,0),new Vector3(0.26f,0.3f,0.26f),glow);
            var warm=GeneratedAssets.Empty("WarmPool",root,new Vector3(-1.5f,3.9f,0.5f)).gameObject.AddComponent<Light>();
            warm.type=LightType.Point;
            warm.color=new Color(1f,0.62f,0.30f);
            warm.intensity=3.4f;
            warm.range=9;
            warm.shadows=LightShadows.None;
            warm.renderMode=LightRenderMode.ForcePixel;
            var fridgeLight=GeneratedAssets.Empty("FridgeWarmFill",root,new Vector3(-4.8f,2.2f,3.5f)).gameObject.AddComponent<Light>();
            fridgeLight.type=LightType.Point;
            fridgeLight.color=new Color(1f,0.65f,0.33f);
            fridgeLight.intensity=1.4f;
            fridgeLight.range=4.5f;
            fridgeLight.shadows=LightShadows.None;
            fridgeLight.renderMode=LightRenderMode.ForceVertex;
        }
        private static void Cup(Transform parent,Vector3 position,float scale)
        {
            Transform cup=P.Part("CeramicCup",parent,cupMesh,ivory,position,Vector3.one*scale);
            P.Rod("HandleTop",cup,new Vector3(0.4f,0.25f,0),new Vector3(0.72f,0.16f,0),0.08f,ivory);
            P.Rod("HandleOuter",cup,new Vector3(0.72f,0.16f,0),new Vector3(0.72f,-0.18f,0),0.08f,ivory);
            P.Rod("HandleBottom",cup,new Vector3(0.72f,-0.18f,0),new Vector3(0.4f,-0.27f,0),0.08f,ivory);
        }
        private static void Bottle(Transform parent,Vector3 position,float scale,bool solid=false)
        {
            Transform bottle=P.Part("Bottle",parent,bottleMesh,amber,position,Vector3.one*scale);
            if(solid)
            {
                var collider=bottle.gameObject.AddComponent<CapsuleCollider>();
                collider.center=new Vector3(0,0.59f,0);
                collider.radius=0.22f;
                collider.height=1.18f;
            }
            P.Box("PaperLabel",parent,position+new Vector3(0,0.42f,-0.213f)*scale,new Vector3(0.3f,0.3f,0.02f)*scale,ivory,false);
        }
        private static void Plant(Transform parent,Vector3 position,float scale)
        {
            Transform plant=GeneratedAssets.Empty("HerbPot",parent,position);
            plant.localScale=Vector3.one*scale;
            P.Part("Pot",plant,cupMesh,soil,new Vector3(0,0.32f,0),new Vector3(0.7f,0.65f,0.7f));
            for(int i=0;i<7;i++)
            {
                Vector3 end=new Vector3(Mathf.Sin(i*2.4f)*0.4f,0.85f+i%3*0.12f,Mathf.Cos(i*2.4f)*0.4f);
                P.Rod("Stem",plant,new Vector3(0,0.5f,0),end,0.023f,leaf);
                Transform blade=P.Sphere("Leaf",plant,end,new Vector3(0.20f,0.06f,0.45f),leaf);
                blade.localRotation=Quaternion.Euler(15,i*137,30);
            }
        }
    }
}
