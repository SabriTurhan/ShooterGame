using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Watermelon
{
    public static class PreviewCamera
    {
        private const float MOVEMENT_SPEED = 40;
        private const Ease.Type MOVEMENT_TWEEN = Ease.Type.CubicInOut;

        private static VirtualCamera previewCamera;
        private static GameObject previewCameraTarget;
        private static GameObject curvatureTarget;

        private static bool isActive;
        public static bool IsActive => isActive;

        private static bool isPaused;
        private static TweenCase mainTweenCase;

        private static Queue<CameraCase> waitingCases = new Queue<CameraCase>();
        private static Queue<CameraCase> finishedCases = new Queue<CameraCase>();

        private static CameraCase frozenCase;

        public static void Initialise()
        {
            // Get preivew virtual camera
            previewCamera = CameraController.GetCamera(CameraType.Preview);

            // Create preview camera target
            previewCameraTarget = new GameObject("[PREVIEW CAMERA TARGET]");
            previewCameraTarget.transform.ResetGlobal();

            // Create custom curvature target
            curvatureTarget = new GameObject("[CURVATURE CUSTOM TARGET]");
            curvatureTarget.transform.ResetGlobal();
        }

        public static void ResetTargetPosition()
        {
            VirtualCamera mainCamera = CameraController.GetCamera(CameraType.Gameplay);

            // Reset camera position
            previewCameraTarget.transform.position = mainCamera.Target.position;
        }

        public static void SetTargetPosition(Vector3 position)
        {
            previewCameraTarget.transform.position = position;
        }

        public static void Focus(Vector3 targetPosition, float freezeTime, SimpleCallback onStart = null, SimpleCallback onFocused = null, SimpleCallback onFreezeTimeEnded = null, SimpleCallback onFinished = null, bool debug = false)
        {
            if (CameraController.IsBlending)
                return;

            if (debug)
            {
                onStart?.Invoke();

                Tween.NextFrame(delegate
                {
                    onFocused?.Invoke();
                    onFreezeTimeEnded?.Invoke();
                    onFinished?.Invoke();

                    Control.EnableMovementControl();
                });

                return;
            }

            CameraCase cameraCase = new CameraCase(targetPosition, freezeTime, onStart, onFocused, onFreezeTimeEnded, onFinished);

            if (!isActive)
            {
                isActive = true;

                VirtualCamera mainCamera = CameraController.GetCamera(CameraType.Gameplay);

                // Reset camera position
                previewCameraTarget.transform.position = mainCamera.Target.position;
                curvatureTarget.transform.position = mainCamera.Target.position;

                // Disable player joystick
                Control.DisableMovementControl();

                // Update transition time
                cameraCase.UpdateMoveTime(mainCamera.Target.position);

                // Start camera movement
                InvokeCase(cameraCase);
            }
            else
            {
                // Add camera case to queue
                waitingCases.Enqueue(cameraCase);
            }
        }

        private static void InvokeCase(CameraCase cameraCase)
        {
            VirtualCamera previewCamera = CameraController.GetCamera(CameraType.Preview);
            previewCamera.SetTarget(previewCameraTarget.transform);

            CameraController.OverrideBlend(CameraType.Gameplay, CameraType.Preview, cameraCase.moveTime, MOVEMENT_TWEEN);

            // Enable Cinemachine tutorial camera
            CameraController.EnableCamera(CameraType.Preview);

            // Invoke camera case start callback
            cameraCase.onStart?.Invoke();

            if (cameraCase.freezeTime < 0)
                frozenCase = cameraCase;

#if MODULE_CURVE
            CurvatureManager.EnableTempTarget(curvatureTarget.transform);

            // Move curvature target for smooth transition
            curvatureTarget.transform.DOMove(cameraCase.targetPosition, cameraCase.moveTime, 0, false, UpdateMethod.Update).SetEasing(MOVEMENT_TWEEN);
#endif

            previewCameraTarget.transform.position = cameraCase.targetPosition;

            Tween.InvokeCoroutine(WaitForCameraBlendToComplete(() =>
            {
                // Invoke camera case focused callback
                cameraCase.onFocused?.Invoke();

                if (cameraCase.freezeTime >= 0)
                    Tween.DelayedCall(cameraCase.freezeTime, () => UnfreezeCase(cameraCase));
            }));
        }

        private static IEnumerator WaitForCameraBlendToComplete(Action OnComplete)
        {
            VirtualCamera previewCamera = CameraController.GetCamera(CameraType.Preview);

            while (previewCamera.IsBlending)
            {
                yield return null;
            }

            OnComplete?.Invoke();
        }

        public static void Unfreeze()
        {
            UnfreezeCase(frozenCase);
        }

        private static void UnfreezeCase(CameraCase cameraCase)
        {
            VirtualCamera mainCamera = CameraController.GetCamera(CameraType.Gameplay);

            if (waitingCases.Count == 0)
            {
                cameraCase.onFreezeTimeEnded?.Invoke();

                CameraController.OverrideBlend(CameraType.Preview, CameraType.Gameplay, cameraCase.moveTime, MOVEMENT_TWEEN);

                CameraController.EnableCamera(CameraType.Gameplay);

#if MODULE_CURVE
                CurvatureManager.EnableTempTarget(curvatureTarget.transform);

                // Move curvature target for smooth transition
                curvatureTarget.transform.DOMove(mainCamera.Target.position, cameraCase.moveTime, 0, false, UpdateMethod.Update).SetEasing(MOVEMENT_TWEEN);
#endif

                Tween.InvokeCoroutine(WaitForCameraBlendToComplete(() =>
                {
                    // Invoke camera case finished callback
                    cameraCase.onFinished?.Invoke();

                    while (finishedCases.Count > 0)
                    {
                        CameraCase finishedCase = finishedCases.Dequeue();
                        if (finishedCase != null)
                        {
                            finishedCase.onFinished?.Invoke();
                        }
                    }

                    if (waitingCases.Count > 0)
                    {
                        CameraCase nextCase = waitingCases.Dequeue();

                        InvokeCase(nextCase);
                    }
                    else
                    {
                        isActive = false;

                        Control.EnableMovementControl();

#if MODULE_CURVE
                        CurvatureManager.DisableTempTarget();
#endif
                    }
                }));
            }
            else
            {
                var nextCase = waitingCases.Dequeue();

                finishedCases.Enqueue(cameraCase);

                InvokeCase(nextCase);
            }

            if (isPaused)
            {
                if (mainTweenCase != null)
                    mainTweenCase.Pause();
            }
        }

        public static void Unload()
        {
            isActive = false;

            frozenCase = null;

            waitingCases.Clear();
            finishedCases.Clear();

            // Enable Cinemachine main camera
            CameraController.EnableCamera(CameraType.Gameplay);
        }

        public static void Pause()
        {
            isPaused = true;

            if (mainTweenCase != null)
                mainTweenCase.Pause();
        }

        public static void Resume()
        {
            isPaused = false;

            if (mainTweenCase != null)
                mainTweenCase.Resume();
        }

        private class CameraCase
        {
            public Vector3 targetPosition;

            public float freezeTime;
            public float moveTime;

            public SimpleCallback onStart;
            public SimpleCallback onFocused;
            public SimpleCallback onFreezeTimeEnded;
            public SimpleCallback onFinished;

            public CameraCase(Vector3 targetPosition, float freezeTime, SimpleCallback onStart = null, SimpleCallback onFocused = null, SimpleCallback onFreezeTimeEnded = null, SimpleCallback onFinished = null)
            {
                this.targetPosition = targetPosition;
                this.freezeTime = freezeTime;

                this.onStart = onStart;
                this.onFocused = onFocused;
                this.onFreezeTimeEnded = onFreezeTimeEnded;
                this.onFinished = onFinished;
            }

            public void UpdateMoveTime(Vector3 startPosition)
            {
                moveTime = Mathf.Clamp(Vector3.Distance(startPosition, targetPosition) / MOVEMENT_SPEED, 0.4f, float.MaxValue);
            }
        }
    }
}