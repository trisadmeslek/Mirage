using System;
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


        private NetworkServer server;

        public IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);

            yield return SceneManager.LoadSceneAsync(testScene);

            // wait 1 frame for start to be called
            yield return null;
            GameObject.Find(LootSpawnerName).GetComponent<Spawner>().count = stationaryCount;
            GameObject.Find(NpcSpawnerName).GetComponent<Spawner>().count = movingCount;


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

            var clients = new NetworkClient[clientCount];
            for (int i = 0; i < clientCount; i++)
            {
                try
                {
                    GameObject clientGo = Instantiate(NetworkManagerPrefab);
                    clients[i] = clientGo.GetComponent<NetworkClient>();
                    clients[i].Connect("localhost");
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            yield return new WaitForSeconds(1);

            Benchmarker.Clear();

            yield return new WaitForSeconds(5);

            Benchmarker.Output();

            server.Stop();
            foreach (NetworkClient client in clients)
            {
                client.Disconnect();
            }

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
