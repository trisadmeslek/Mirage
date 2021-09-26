using System.Collections;
using Mirage.Examples.InterestManagement;
using Mirage.InterestManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirage.Tests.Performance.Runtime
{
    public class InterestManagementPerformanceBase_BenchmarkRunner : MonoBehaviour
    {
        const string testScene = "Assets/Examples/InterestManagement/Scenes/Scene.unity";
        const string NpcSpawnerName = "Mobs";
        const string LootSpawnerName = "Ground";
        [SerializeField] GameObject NetworkManagerPrefab;
        [SerializeField] int clientCount = 100;
        [SerializeField] int stationaryCount = 3500;
        [SerializeField] int movingCount = 500;

        [SerializeField] bool runServer;

        private NetworkServer server;

        public IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;

            string[] args = System.Environment.GetCommandLineArgs();
            if (runServer || args[1] == "server")
            {
                yield return RunServer();
            }
            else
            {
                yield return RunClient();
            }
        }


        public IEnumerator RunClient()
        {
            Debug.Log("RunClient");

            yield return SceneManager.LoadSceneAsync(testScene);

            // wait 1 frame for start to be called
            yield return null;

            NetworkManager manager = FindObjectOfType<NetworkManager>();
            NetworkClient client = manager.Client;

            client.Connect();

            while (!client.IsConnected) { yield return null; }

            yield return new WaitForSeconds(5);

            while (client.IsConnected) { yield return null; }
            Debug.Log("Exit");

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public IEnumerator RunServer()
        {
            Debug.Log("RunServer");
            yield return SceneManager.LoadSceneAsync(testScene);

            // wait 1 frame for start to be called
            yield return null;
            Spawner lootSpawner = GameObject.Find(LootSpawnerName).GetComponent<Spawner>();
            Spawner npcSpawner = GameObject.Find(NpcSpawnerName).GetComponent<Spawner>();
            lootSpawner.count = stationaryCount;
            npcSpawner.count = movingCount;

            NetworkManager manager = FindObjectOfType<NetworkManager>();
            server = manager.Server;

            bool started = false;
            server.MaxConnections = clientCount;

            // wait frame for destroy
            yield return null;

            server.Started.AddListener(() => started = true);
            server.StartServer();

            // wait for start
            while (!started) { yield return null; }
            lootSpawner.Spawn();
            npcSpawner.Spawn();

            // wait for 10 players
            while (server.Players.Count < clientCount) { yield return null; }

            //var clients = new NetworkClient[clientCount];
            //for (int i = 0; i < clientCount; i++)
            //{
            //    try
            //    {
            //        GameObject clientGo = Instantiate(NetworkManagerPrefab);
            //        clients[i] = clientGo.GetComponent<NetworkClient>();
            //        clients[i].Connect("localhost");
            //    }
            //    catch (Exception ex)
            //    {
            //        Debug.LogException(ex);
            //    }
            //}

            yield return new WaitForSeconds(1);

            Benchmarker.Clear();

            yield return new WaitForSeconds(5);

            Benchmarker.Output();

            server.Stop();
            //foreach (NetworkClient client in clients)
            //{
            //    client.Disconnect();
            //}

            Debug.Log("Exit");
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
