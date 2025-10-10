using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace Watermelon
{
    [DefaultExecutionOrder(100)]
    public sealed class CameraController : MonoBehaviour, ISceneSavingCallback
    {
        private static CameraController cameraController;

        [SerializeField] CameraType firstCamera;

        [Space]
        [ReadOnly]
        [SerializeField] VirtualCamera[] virtualCameras;

        [Header("Blends")]
        [SerializeField] List<CameraBlendSettings> blendSettings;

        [UnpackNested]
        [SerializeField] CameraBlendData defaultBlendData;

        private static Transform cameraTransform;

        private static Dictionary<CameraType, int> virtualCamerasLink;

        private static Camera mainCamera;
        public static Camera MainCamera => mainCamera;

        private static VirtualCamera activeCamera;
        public static VirtualCamera ActiveVirtualCamera => activeCamera;

        private static bool isBlending;
        public static bool IsBlending => isBlending;

        private static CameraBlendCase currentBlendCase;

        public void Initialise()
        {
            cameraController = this;

            // Get camera component
            mainCamera = GetComponent<Camera>();
            cameraTransform = transform;

            // Initialise cameras link
            virtualCamerasLink = new Dictionary<CameraType, int>();
            for (int i = 0; i < virtualCameras.Length; i++)
            {
                virtualCameras[i].Init();

                virtualCamerasLink.Add(virtualCameras[i].CameraType, i);
            }

            VirtualCamera firstVirtualCamera = GetCamera(firstCamera);
            firstVirtualCamera.Activate();

            activeCamera = firstVirtualCamera;

            UpdateCamera();
        }

        private static void UpdateCamera()
        {
            UpdateCamera(activeCamera.CameraData);
        }

        private static void UpdateCamera(CameraLocalData cameraData)
        {
            if (activeCamera.Target == null)
                return;

            mainCamera.fieldOfView = cameraData.FieldOfView;
            mainCamera.nearClipPlane = cameraData.NearClipPlane;
            mainCamera.farClipPlane = cameraData.FarClipPlane;

            cameraTransform.SetPositionAndRotation(cameraData.Position, cameraData.Rotation);
        }

        private void LateUpdate()
        {
            if (activeCamera == null)
                return;

            // Update camera position
            if (isBlending)
            {
                UpdateCamera(currentBlendCase.CameraData);

                return;
            }

            UpdateCamera();
        }

        public static VirtualCamera GetCamera(CameraType cameraType)
        {
            return cameraController.virtualCameras[virtualCamerasLink[cameraType]];
        }

        private static CameraBlendData GetBlendData(CameraType firstCameraType, CameraType secondCameraType)
        {
            for (int i = 0; i < cameraController.blendSettings.Count; i++)
            {
                if (cameraController.blendSettings[i].FirstCameraType == firstCameraType && cameraController.blendSettings[i].SecondCameraType == secondCameraType)
                {
                    return cameraController.blendSettings[i].BlendData;
                }
            }

            return cameraController.defaultBlendData;
        }

        public static void EnableCamera(CameraType cameraTypeToEnable)
        {
            // if required camera is already active
            if (activeCamera != null && activeCamera.CameraType == cameraTypeToEnable)
                return;

            // if required camera doesn't exist
            VirtualCamera newCamera = GetCamera(cameraTypeToEnable);
            if (newCamera == null)
            {
                Debug.LogError($"Camera of type {cameraTypeToEnable} not found.");

                return;
            }

            // if there was no camera - instantly activate required
            if (activeCamera == null)
            {
                activeCamera = newCamera;
                activeCamera.Activate();

                UpdateCamera();

                return;
            }

            CameraType currentCameraType = activeCamera.CameraType;

            // Get blend data
            CameraBlendData blendData = GetBlendData(currentCameraType, cameraTypeToEnable);

            // if blend time is zero - disable current camera and activate required
            if (blendData.BlendTime <= 0)
            {
                activeCamera.Disable();

                activeCamera = newCamera;
                activeCamera.Activate();

                UpdateCamera();

                return;
            }

            isBlending = true;

            newCamera.Activate();

            // running blend
            currentBlendCase = new CameraBlendCase(activeCamera, newCamera, blendData, () =>
            {
                activeCamera.Disable();
                activeCamera = newCamera;
                activeCamera.Activate();

                isBlending = false;
            });
        }

        public static void OverrideBlend(CameraType firstCameraType, CameraType secondCameraType, float newTime, Ease.Type easing)
        {
            if (cameraController.blendSettings.FindIndex(s => s.FirstCameraType == firstCameraType && s.SecondCameraType == secondCameraType) != -1)
            {
                CameraBlendData blendData = GetBlendData(firstCameraType, secondCameraType);
                blendData.OverrideBlendTime(newTime);
            }
            else
            {
                CameraBlendSettings newBlend = new CameraBlendSettings(firstCameraType, secondCameraType, new CameraBlendData(newTime, easing));
                cameraController.blendSettings.Add(newBlend);
            }
        }

        public void OnSceneSaving()
        {
            VirtualCamera[] cachedVirtualCameras = FindObjectsByType<VirtualCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (!cachedVirtualCameras.SafeSequenceEqual(virtualCameras))
            {
                virtualCameras = cachedVirtualCameras;

                RuntimeEditorUtils.SetDirty(this);
            }
        }
    }

    public class CameraBlendCase
    {
        public readonly VirtualCamera FirstCamera;
        public readonly VirtualCamera SecondCamera;

        private CameraBlendData cameraBlendData;

        private Ease.IEasingFunction easingFunction;

        private TweenCase tweenCase;

        private CameraLocalData cameraData;
        public CameraLocalData CameraData => cameraData;

        public CameraBlendCase(VirtualCamera firstCamera, VirtualCamera secondCamera, CameraBlendData cameraBlendData, SimpleCallback completeCallback)
        {
            this.cameraBlendData = cameraBlendData;

            FirstCamera = firstCamera;
            SecondCamera = secondCamera;

            firstCamera.StartTransition();
            secondCamera.StartTransition();

            cameraData = new CameraLocalData(firstCamera.CameraData);
            easingFunction = Ease.GetFunction(cameraBlendData.BlendEaseType);

            tweenCase = Tween.DoFloat(0f, 1.0f, cameraBlendData.BlendTime, (value) =>
            {
                cameraData.Lerp(firstCamera.CameraData, secondCamera.CameraData, value);

            }).SetCustomEasing(easingFunction).OnComplete(() =>
            {
                FirstCamera.StopTransition();
                SecondCamera.StopTransition();

                completeCallback?.Invoke();
            });
        }

        public void Clear()
        {
            tweenCase.KillActive();
        }
    }
}