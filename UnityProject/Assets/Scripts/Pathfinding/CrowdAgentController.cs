using UnityEngine;

namespace Minecraft.Pathfinding
{
    [DisallowMultipleComponent]
    public class CrowdAgentController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("水平移动速度")]
        public float MoveSpeed = 4f;

        [Tooltip("竖直移动速度")]
        public float VerticalMoveSpeed = 6f;

        [Tooltip("朝向插值速度")]
        public float RotationSpeed = 10f;

        [Tooltip("枢轴到脚底的高度")]
        public float PivotHeightFromFeet = 1f;

        [Tooltip("节点判定距离")]
        public float NodeReachDistance = 0.08f;

        [System.NonSerialized] private Vector3Int? m_TargetBlock;

        public bool HasTarget => m_TargetBlock.HasValue;
        public Vector3Int TargetBlock => m_TargetBlock ?? default;

        public void SetTarget(Vector3Int target) => m_TargetBlock = target;

        public void ClearTarget() => m_TargetBlock = null;

        public Vector3Int GetCurrentGridPosition()
        {
            Vector3 pos = transform.position;
            float feetY = pos.y - PivotHeightFromFeet + 0.01f;
            return new Vector3Int(
                Mathf.FloorToInt(pos.x),
                Mathf.FloorToInt(feetY),
                Mathf.FloorToInt(pos.z));
        }

        public void MoveTowardsNode(Vector3Int nextNode, float deltaTime)
        {
            Vector3 targetPos = new Vector3(
                nextNode.x + 0.5f,
                nextNode.y + PivotHeightFromFeet,
                nextNode.z + 0.5f);

            Vector3 current = transform.position;
            Vector3 toTarget = targetPos - current;

            Vector3 planar = new Vector3(toTarget.x, 0f, toTarget.z);
            float planarDistance = planar.magnitude;

            if (planarDistance > NodeReachDistance)
            {
                Vector3 planarMove = planar.normalized * (MoveSpeed * deltaTime);
                if (planarMove.magnitude > planarDistance)
                {
                    planarMove = planar;
                }

                current += planarMove;
            }
            else
            {
                current.x = targetPos.x;
                current.z = targetPos.z;
            }

            current.y = Mathf.MoveTowards(current.y, targetPos.y, VerticalMoveSpeed * deltaTime);
            transform.position = current;

            if (planar.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(planar.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, RotationSpeed * deltaTime);
            }
        }
    }
}
