// ------------------------------------------------------------
// CameraFollow2D.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 2D 镜头跟随：LateUpdate 平滑跟随目标（玩家）。
    /// 设了边界（_maxBounds > _minBounds）时，镜头中心按正交视野半宽/半高 clamp，
    /// 到边缘停止跟随、不越过关卡边界（用户需求：大场景跟随 + 防出界）。
    /// 挂在 **CameraRig**（相机父物体）上、移动 Rig；子相机由 CameraShake2D 做局部抖动，两者互不抢 transform。
    /// _cam 指向子相机（用于取正交视野做 clamp）。SetTarget 注入玩家。
    /// </summary>
    public class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Camera _cam;   // 子相机，用于取正交视野半宽/半高做边界 clamp
        [SerializeField] private float _smoothTime = 0.15f;
        [SerializeField] private Vector2 _offset = Vector2.zero;

        [Header("边界 clamp（_maxBounds 各分量 > _minBounds 才生效）")]
        [SerializeField] private Vector2 _minBounds = Vector2.zero;
        [SerializeField] private Vector2 _maxBounds = Vector2.zero;

        private Vector3 _velocity;

        private void Awake()
        {
            if (_cam == null)
            {
                _cam = GetComponent<Camera>(); // 兼容直接挂相机上的情况
            }
        }

        /// <summary>注入跟随目标（玩家）。生成器 / GameManager 接线用。</summary>
        public void SetTarget(Transform target)
        {
            _target = target;
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            float z = transform.position.z; // 保持相机 z（正交前后位置）
            Vector3 desired = new Vector3(_target.position.x + _offset.x, _target.position.y + _offset.y, z);

            // 边界 clamp：镜头中心不越界（考虑正交视野半宽/半高）→ 到边缘停止跟随、防出界。
            if (_cam != null && _cam.orthographic &&
                _maxBounds.x > _minBounds.x && _maxBounds.y > _minBounds.y)
            {
                float halfH = _cam.orthographicSize;
                float halfW = halfH * _cam.aspect;

                float minX = _minBounds.x + halfW;
                float maxX = _maxBounds.x - halfW;
                float minY = _minBounds.y + halfH;
                float maxY = _maxBounds.y - halfH;

                // 若边界比视野还小，居中显示；否则 clamp。
                desired.x = minX <= maxX ? Mathf.Clamp(desired.x, minX, maxX) : (_minBounds.x + _maxBounds.x) * 0.5f;
                desired.y = minY <= maxY ? Mathf.Clamp(desired.y, minY, maxY) : (_minBounds.y + _maxBounds.y) * 0.5f;
            }

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, _smoothTime);
        }
    }
}
