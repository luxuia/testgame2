using System;
using Minecraft;
using Minecraft.Configurations;
using Minecraft.Jobs;
using UnityEngine;

namespace Minecraft.Entities
{
    [DisallowMultipleComponent]
    public class WorkerEntity : Entity
    {
        [SerializeField] private float m_MoveSpeed = 3f;
        [SerializeField] private float m_WorkSpeed = 1f;

        [NonSerialized] private BlockJob m_CurrentJob;
        [NonSerialized] private bool m_UseGravityOverride;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            m_UseGravityOverride = UseGravity;
        }

        protected override void FixedUpdate()
        {
            if (m_CurrentJob == null)
            {
                base.FixedUpdate();
                return;
            }

            UseGravity = false;
            Vector3 targetPos = new Vector3(m_CurrentJob.Position.x + 0.5f, m_CurrentJob.Position.y + 1f, m_CurrentJob.Position.z + 0.5f);
            float dist = Vector3.Distance(Position, targetPos);

            if (dist > 0.2f)
            {
                Vector3 dir = (targetPos - Position).normalized;
                Position += dir * m_MoveSpeed * Time.fixedDeltaTime;
                return;
            }

            m_CurrentJob.IsBeingWorked = true;
            m_CurrentJob.WorkTimeDone += m_WorkSpeed * Time.fixedDeltaTime;

            if (m_CurrentJob.WorkTimeDone >= m_CurrentJob.WorkTimeRequired)
            {
                ExecuteJob(m_CurrentJob);
                m_CurrentJob.IsCompleted = true;
                m_CurrentJob.IsBeingWorked = false;
                if (JobManager.Instance != null)
                {
                    JobManager.Instance.Dequeue(m_CurrentJob);
                }
                m_CurrentJob = null;
                UseGravity = m_UseGravityOverride;
            }

            base.FixedUpdate();
        }

        private void Update()
        {
            if (m_CurrentJob != null) return;
            if (JobManager.Instance == null || World == null) return;

            m_CurrentJob = JobManager.Instance.GetJob(Position);
        }

        private void ExecuteJob(BlockJob job)
        {
            if (World == null || !World.RWAccessor.Accessible) return;

            int x = job.Position.x;
            int y = job.Position.y;
            int z = job.Position.z;

            if (job.JobType == BlockJobType.Build)
            {
                BlockData block = World.BlockDataTable.GetBlock(job.BlockType);
                if (block != null)
                {
                    World.RWAccessor.SetBlock(x, y, z, block, Quaternion.identity, ModificationSource.PlayerAction);
                }
            }
            else if (job.JobType == BlockJobType.Deconstruct)
            {
                BlockData airBlock = World.BlockDataTable.GetBlock(0);
                World.RWAccessor.SetBlock(x, y, z, airBlock ?? World.BlockDataTable.GetBlock(0), Quaternion.identity, ModificationSource.PlayerAction);
            }
        }
    }
}
