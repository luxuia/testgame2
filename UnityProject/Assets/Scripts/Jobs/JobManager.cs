using System.Collections.Generic;
using UnityEngine;

namespace Minecraft.Jobs
{
    public class JobManager : MonoBehaviour
    {
        private static JobManager s_Instance;
        public static JobManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    var go = new GameObject("JobManager");
                    s_Instance = go.AddComponent<JobManager>();
                    DontDestroyOnLoad(go);
                }
                return s_Instance;
            }
            private set => s_Instance = value;
        }

        private readonly Queue<BlockJob> m_JobQueue = new Queue<BlockJob>();

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        public void Enqueue(BlockJob job)
        {
            if (job == null) return;
            m_JobQueue.Enqueue(job);
        }

        public BlockJob GetJob(Vector3 workerPosition)
        {
            if (m_JobQueue.Count == 0) return null;

            BlockJob best = null;
            float bestDist = float.MaxValue;

            foreach (var job in m_JobQueue)
            {
                if (job.IsBeingWorked || job.IsCompleted) continue;

                float dist = Vector3.Distance(workerPosition, new Vector3(job.Position.x + 0.5f, job.Position.y + 0.5f, job.Position.z + 0.5f));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = job;
                }
            }
            return best;
        }

        public void Dequeue(BlockJob job)
        {
            var list = new List<BlockJob>(m_JobQueue);
            list.Remove(job);
            m_JobQueue.Clear();
            foreach (var j in list)
            {
                m_JobQueue.Enqueue(j);
            }
        }

        public int PendingCount => m_JobQueue.Count;
    }
}
