using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FoundersFirstCivilization.Editor
{
    public static class IslandPrototypeGenerator
    {
        const int HeightResolution = 513;
        const int AlphaResolution = 256;
        const float IslandSize = 1000f;
        const float Height = 230f;
        const float SeaLevel = 18f;
        static readonly Color Ocean = new Color(0.11f, 0.32f, 0.42f);
        static readonly Vector2[] RiverRoute = {
            new Vector2(0.40f, 0.64f), new Vector2(0.47f, 0.62f),
            new Vector2(0.53f, 0.60f), new Vector2(0.59f, 0.56f),
            new Vector2(0.68f, 0.55f), new Vector2(0.77f, 0.55f),
            new Vector2(0.87f, 0.56f), new Vector2(0.96f, 0.56f)
        };

        [MenuItem("Founders/Generate Island Prototype")]
        public static void Generate()
        {
            EnsureFolder("Assets/Generated");
            EnsureFolder("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var terrain = BuildTerrain();
            BuildOcean();
            BuildLakeAndRiver(terrain);
            BuildVolcano();
            BuildWilds(terrain);
            BuildLighting();
            BuildCamera();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Island.unity");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Island scene saved to Assets/Scenes/Island.unity");
        }

        static Terrain BuildTerrain()
        {
            var data = new TerrainData {
                heightmapResolution = HeightResolution,
                alphamapResolution = AlphaResolution,
                baseMapResolution = 1024,
                size = new Vector3(IslandSize, Height, IslandSize)
            };
            var heights = new float[HeightResolution, HeightResolution];
            for (int z = 0; z < HeightResolution; z++)
            for (int x = 0; x < HeightResolution; x++)
            {
                float u = x / (float)(HeightResolution - 1);
                float v = z / (float)(HeightResolution - 1);
                float dx = (u - 0.5f) / 0.47f;
                float dz = (v - 0.5f) / 0.43f;
                float radius = Mathf.Sqrt(dx * dx + dz * dz);
                float coast = 1f - Mathf.SmoothStep(0.79f, 1.08f, radius);
                float land = 0.10f
                    + 0.010f * Mathf.PerlinNoise(u * 12f + 2f, v * 12f)
                    + 0.006f * Mathf.PerlinNoise(u * 29f, v * 29f + 10f);

                // Rugged mountain chain, with high snow line and mineral colored rock.
                land += Gauss(u, v, 0.40f, 0.69f, 0.145f, 0.09f, 0.49f);
                land += Gauss(u, v, 0.48f, 0.70f, 0.085f, 0.06f, 0.31f);
                land += Gauss(u, v, 0.36f, 0.65f, 0.075f, 0.07f, 0.23f);

                // Dormant volcano on the far eastern headland.
                float cone = Mathf.Clamp01(1f - Distance(u, v, 0.78f, 0.45f) / 0.16f);
                land += cone * cone * 0.35f;
                land -= Gauss(u, v, 0.78f, 0.45f, 0.026f, 0.022f, 0.055f);

                // Recessed lake basin and a narrow valley from the mountain.
                land -= Gauss(u, v, 0.59f, 0.56f, 0.055f, 0.043f, 0.026f);
                float riverDistance = DistanceToRiver(u, v);
                land -= Mathf.Exp(-(riverDistance * riverDistance) / 0.00014f) * 0.085f;

                heights[z, x] = Mathf.Clamp01(SeaLevel / Height + land * coast);
            }
            data.SetHeights(0, 0, heights);
            data.terrainLayers = MakeLayers();

            var alpha = new float[AlphaResolution, AlphaResolution, 5];
            for (int z = 0; z < AlphaResolution; z++)
            for (int x = 0; x < AlphaResolution; x++)
            {
                float u = x / (float)(AlphaResolution - 1);
                float v = z / (float)(AlphaResolution - 1);
                float h = data.GetInterpolatedHeight(u, v);
                float slope = 1f - data.GetInterpolatedNormal(u, v).y;
                float ash = Distance(u, v, 0.78f, 0.45f) < 0.20f ? 0.9f : 0f;
                float snow = Mathf.Clamp01((h - 152f) / 30f);
                float rock = Mathf.Clamp01(slope * 2.4f + (h - 70f) / 150f);
                float sand = Mathf.Clamp01(1f - (h - SeaLevel) / 12f);
                float grass = Mathf.Clamp01(1f - Mathf.Max(rock, snow) - ash);
                float sum = Mathf.Max(0.001f, grass + sand + rock + snow + ash);
                alpha[z, x, 0] = grass / sum;
                alpha[z, x, 1] = sand / sum;
                alpha[z, x, 2] = rock / sum;
                alpha[z, x, 3] = snow / sum;
                alpha[z, x, 4] = ash / sum;
            }
            data.SetAlphamaps(0, 0, alpha);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Island | editable terrain";
            go.transform.position = new Vector3(-IslandSize / 2f, 0f, -IslandSize / 2f);
            var t = go.GetComponent<Terrain>();
            t.heightmapPixelError = 8f;
            t.basemapDistance = 1600f;
            t.drawInstanced = true;
            AssetDatabase.CreateAsset(data, "Assets/Generated/IslandTerrain.asset");
            return t;
        }

        static TerrainLayer[] MakeLayers()
        {
            Color[] colors = {
                new Color(0.34f, 0.47f, 0.26f),
                new Color(0.70f, 0.61f, 0.41f),
                new Color(0.34f, 0.34f, 0.31f),
                new Color(0.79f, 0.81f, 0.80f),
                new Color(0.24f, 0.22f, 0.20f)
            };
            string[] names = { "Meadow", "CoastalSand", "MountainStone", "HighPeak", "VolcanicAsh" };
            var layers = new TerrainLayer[colors.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                var tex = new Texture2D(32, 32, TextureFormat.RGB24, true) { name = names[i] + " texture" };
                tex.wrapMode = TextureWrapMode.Repeat;
                var pixels = new Color[32 * 32];
                for (int p = 0; p < pixels.Length; p++)
                    pixels[p] = colors[i] * Random.Range(0.91f, 1.09f);
                tex.SetPixels(pixels);
                tex.Apply(true);
                string stem = "Assets/Generated/" + names[i];
                AssetDatabase.CreateAsset(tex, stem + "Texture.asset");
                var layer = new TerrainLayer {
                    name = names[i],
                    diffuseTexture = tex,
                    tileSize = new Vector2(18f, 18f)
                };
                AssetDatabase.CreateAsset(layer, stem + ".terrainlayer");
                layers[i] = layer;
            }
            return layers;
        }

        static void BuildOcean()
        {
            var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ocean.name = "Ocean | future docks and offshore structures";
            ocean.transform.position = new Vector3(0f, SeaLevel, 0f);
            ocean.transform.localScale = new Vector3(115f, 1f, 115f);
            Materialize(ocean, "Ocean", Ocean, 0.8f);
        }

        static void BuildLakeAndRiver(Terrain terrain)
        {
            var lake = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lake.name = "Lake | river basin";
            Vector3 lakeCenter = World(0.59f, 0.56f);
            lake.transform.position = lakeCenter + Vector3.up * (terrain.SampleHeight(lakeCenter) + 0.3f);
            lake.transform.localScale = new Vector3(48f, 0.45f, 34f);
            Materialize(lake, "Lake water", new Color(0.20f, 0.48f, 0.58f), 0.8f);

            Vector3[] path = new Vector3[RiverRoute.Length];
            for (int i = 0; i < RiverRoute.Length; i++)
            {
                Vector3 point = World(RiverRoute[i].x, RiverRoute[i].y);
                path[i] = point + Vector3.up * (terrain.SampleHeight(point) + 0.8f);
            }
            CreateWaterSegments("River | waterfall, lake and ocean", path, 8f,
                new Color(0.20f, 0.48f, 0.58f));

            var fall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fall.name = "Great waterfall | mountain spring";
            fall.transform.position = new Vector3(-100f, 132f, 190f);
            fall.transform.localScale = new Vector3(28f, 64f, 3f);
            Materialize(fall, "Waterfall", new Color(0.62f, 0.83f, 0.86f), 0.3f);

            var spray = new GameObject("Waterfall spray");
            spray.transform.position = new Vector3(-100f, 99f, 192f);
            var particles = spray.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startLifetime = 1.1f;
            main.startSpeed = 4f;
            main.startSize = 1.8f;
            main.startColor = new Color(0.8f, 0.92f, 0.94f, 0.55f);
            main.maxParticles = 500;
            var emission = particles.emission;
            emission.rateOverTime = 60f;
        }

        static void BuildVolcano()
        {
            Vector3[] points = {
                new Vector3(280f, 105f, -50f),
                new Vector3(296f, 88f, -48f),
                new Vector3(311f, 70f, -66f),
                new Vector3(329f, 50f, -84f)
            };
            CreateWaterSegments("Lava", points, 6f, new Color(1f, 0.25f, 0.035f));
            var crater = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crater.name = "Dormant volcano crater";
            crater.transform.position = new Vector3(280f, 108f, -50f);
            crater.transform.localScale = new Vector3(20f, 1f, 20f);
            Materialize(crater, "Crater stone", new Color(0.22f, 0.20f, 0.19f), 0.9f);
        }

        static void BuildWilds(Terrain terrain)
        {
            var root = new GameObject("Wilds | individually removable");
            var bark = NewMaterial("Bark", new Color(0.30f, 0.22f, 0.15f));
            var leaf = NewMaterial("Canopy", new Color(0.20f, 0.36f, 0.20f));
            var stone = NewMaterial("Field stone", new Color(0.43f, 0.43f, 0.39f));
            var petals = new[] {
                NewMaterial("Gold flowers", new Color(0.92f, 0.67f, 0.20f)),
                NewMaterial("Lavender flowers", new Color(0.59f, 0.48f, 0.72f)),
                NewMaterial("Cream flowers", new Color(0.91f, 0.86f, 0.65f))
            };

            Random.InitState(1834);
            int trees = 0, attempts = 0;
            while (trees < 72 && attempts++ < 700)
            {
                float u = Random.Range(0.12f, 0.89f);
                float v = Random.Range(0.12f, 0.88f);
                if (Distance(u, v, 0.78f, 0.45f) < 0.20f ||
                    Distance(u, v, 0.40f, 0.69f) < 0.17f ||
                    Distance(u, v, 0.59f, 0.56f) < 0.07f) continue;
                Vector3 p = World(u, v);
                float y = terrain.SampleHeight(p);
                if (y < SeaLevel + 4f || y > 102f) continue;
                if (Random.value > 0.76f && Distance(u, v, 0.48f, 0.70f) > 0.21f) continue;
                MakeTree(root.transform, p + Vector3.up * y, Random.Range(0.8f, 1.25f), bark, leaf);
                trees++;
            }

            for (int i = 0; i < 36; i++)
            {
                float u = Random.Range(0.1f, 0.9f), v = Random.Range(0.12f, 0.88f);
                Vector3 p = World(u, v);
                float y = terrain.SampleHeight(p);
                if (y < SeaLevel + 3f || y > 130f) continue;
                var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = "Loose stone | removable";
                rock.transform.SetParent(root.transform);
                rock.transform.position = p + Vector3.up * y;
                rock.transform.localScale = new Vector3(Random.Range(2f, 5f), Random.Range(1f, 3f), Random.Range(2f, 4f));
                rock.transform.rotation = Random.rotation;
                rock.GetComponent<Renderer>().sharedMaterial = stone;
            }

            for (int i = 0; i < 80; i++)
            {
                float u = Random.Range(0.12f, 0.89f), v = Random.Range(0.12f, 0.88f);
                Vector3 p = World(u, v);
                float y = terrain.SampleHeight(p);
                if (y < SeaLevel + 3f || y > 88f) continue;
                var flower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flower.name = "Wildflower | removable";
                flower.transform.SetParent(root.transform);
                flower.transform.position = p + Vector3.up * (y + 0.55f);
                flower.transform.localScale = Vector3.one * Random.Range(0.7f, 1.1f);
                flower.GetComponent<Renderer>().sharedMaterial = petals[Random.Range(0, petals.Length)];
            }
        }

        static void MakeTree(Transform parent, Vector3 p, float scale, Material bark, Material leaves)
        {
            var tree = new GameObject("Broadleaf tree | removable");
            tree.transform.SetParent(parent);
            tree.transform.position = p;
            tree.transform.localScale = Vector3.one * scale;
            Primitive(tree.transform, PrimitiveType.Cylinder, "Trunk",
                new Vector3(0f, 3.5f, 0f), new Vector3(0.65f, 3.5f, 0.65f), bark);
            for (int i = 0; i < 3; i++)
                Primitive(tree.transform, PrimitiveType.Sphere, "Leaf crown",
                    new Vector3(Random.Range(-0.8f, 0.8f), 6.3f + i * 1.45f, Random.Range(-0.8f, 0.8f)),
                    Vector3.one * (4.2f - i * 0.45f), leaves);
        }

        static GameObject Primitive(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static void CreateWaterSegments(string groupName, Vector3[] points, float width, Color color)
        {
            var group = new GameObject(groupName);
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = "Water segment " + (i + 1);
                segment.transform.SetParent(group.transform);
                segment.transform.position = (a + b) * 0.5f;
                segment.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
                segment.transform.localScale = new Vector3(width, 0.55f, Vector3.Distance(a, b) + 1f);
                Materialize(segment, groupName + " material", color, 0.7f);
            }
        }

        static void BuildLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.61f, 0.73f, 0.79f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.51f, 0.40f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.24f, 0.20f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.70f, 0.77f, 0.74f);
            RenderSettings.fogDensity = 0.00055f;
            var sun = new GameObject("Sun | warm morning light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.91f, 0.74f);
            sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
        }

        static void BuildCamera()
        {
            var go = new GameObject("Island overview camera");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 46f;
            camera.farClipPlane = 3000f;
            camera.backgroundColor = Ocean;
            camera.clearFlags = CameraClearFlags.SolidColor;
            go.transform.position = new Vector3(-25f, 700f, -940f);
            go.transform.rotation = Quaternion.Euler(34f, -1f, 0f);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] bits = path.Split('/');
            string parent = bits[0];
            for (int i = 1; i < bits.Length; i++)
            {
                string next = parent + "/" + bits[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, bits[i]);
                parent = next;
            }
        }

        static Material NewMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Standard"));
            material.name = name;
            material.color = color;
            return material;
        }

        static void Materialize(GameObject go, string name, Color color, float gloss)
        {
            var material = NewMaterial(name, color);
            material.SetFloat("_Glossiness", gloss);
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        static Vector3 World(float u, float v)
            => new Vector3(u * IslandSize - IslandSize / 2f, 0f, v * IslandSize - IslandSize / 2f);

        static float Gauss(float x, float y, float cx, float cy, float sx, float sy, float peak)
        {
            float dx = (x - cx) / sx, dy = (y - cy) / sy;
            return Mathf.Exp(-0.5f * (dx * dx + dy * dy)) * peak;
        }

        static float DistanceToRiver(float x, float y)
        {
            var point = new Vector2(x, y);
            float nearest = float.MaxValue;
            for (int i = 0; i < RiverRoute.Length - 1; i++)
            {
                Vector2 a = RiverRoute[i], b = RiverRoute[i + 1];
                Vector2 segment = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude);
                nearest = Mathf.Min(nearest, Vector2.Distance(point, a + t * segment));
            }
            return nearest;
        }

        static float Distance(float x, float y, float cx, float cy)
            => Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
    }
}