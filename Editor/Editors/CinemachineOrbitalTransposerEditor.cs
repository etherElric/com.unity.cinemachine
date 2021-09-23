using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Cinemachine.Editor
{
    [CustomEditor(typeof(CinemachineOrbitalTransposer))]
    [CanEditMultipleObjects]
    internal class CinemachineOrbitalTransposerEditor : BaseEditor<CinemachineOrbitalTransposer>
    {
        /// <summary>Get the property names to exclude in the inspector.</summary>
        /// <param name="excluded">Add the names to this list</param>
        protected override void GetExcludedPropertiesInInspector(List<string> excluded)
        {
            base.GetExcludedPropertiesInInspector(excluded);
            if (Target.m_HeadingIsSlave)
            {
                excluded.Add(FieldPath(x => x.m_FollowOffset));
                excluded.Add(FieldPath(x => x.m_BindingMode));
                excluded.Add(FieldPath(x => x.m_Heading));
                excluded.Add(FieldPath(x => x.m_XAxis));
                excluded.Add(FieldPath(x => x.m_RecenterToTargetHeading));
            }
            if (Target.m_HideOffsetInInspector)
                excluded.Add(FieldPath(x => x.m_FollowOffset));

            switch (Target.m_BindingMode)
            {
                default:
                case CinemachineTransposer.BindingMode.LockToTarget:
                    if (Target.m_AngularDampingMode == CinemachineTransposer.AngularDampingMode.Euler)
                        excluded.Add(FieldPath(x => x.m_AngularDamping));
                    else
                    {
                        excluded.Add(FieldPath(x => x.m_PitchDamping));
                        excluded.Add(FieldPath(x => x.m_YawDamping));
                        excluded.Add(FieldPath(x => x.m_RollDamping));
                    }
                    break;
                case CinemachineTransposer.BindingMode.LockToTargetNoRoll:
                    excluded.Add(FieldPath(x => x.m_RollDamping));
                    excluded.Add(FieldPath(x => x.m_AngularDamping));
                    excluded.Add(FieldPath(x => x.m_AngularDampingMode));
                    break;
                case CinemachineTransposer.BindingMode.LockToTargetWithWorldUp:
                    excluded.Add(FieldPath(x => x.m_PitchDamping));
                    excluded.Add(FieldPath(x => x.m_RollDamping));
                    excluded.Add(FieldPath(x => x.m_AngularDamping));
                    excluded.Add(FieldPath(x => x.m_AngularDampingMode));
                    break;
                case CinemachineTransposer.BindingMode.LockToTargetOnAssign:
                case CinemachineTransposer.BindingMode.WorldSpace:
                    excluded.Add(FieldPath(x => x.m_PitchDamping));
                    excluded.Add(FieldPath(x => x.m_YawDamping));
                    excluded.Add(FieldPath(x => x.m_RollDamping));
                    excluded.Add(FieldPath(x => x.m_AngularDamping));
                    excluded.Add(FieldPath(x => x.m_AngularDampingMode));
                    break;
                case CinemachineTransposer.BindingMode.SimpleFollowWithWorldUp:
                    excluded.Add(FieldPath(x => x.m_XDamping));
                    excluded.Add(FieldPath(x => x.m_PitchDamping));
                    excluded.Add(FieldPath(x => x.m_YawDamping));
                    excluded.Add(FieldPath(x => x.m_RollDamping));
                    excluded.Add(FieldPath(x => x.m_AngularDamping));
                    excluded.Add(FieldPath(x => x.m_AngularDampingMode));
                    excluded.Add(FieldPath(x => x.m_Heading));
                    excluded.Add(FieldPath(x => x.m_RecenterToTargetHeading));
                    break;
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < targets.Length; ++i)
                (targets[i] as CinemachineOrbitalTransposer).UpdateInputAxisProvider();
        }

        public override void OnInspectorGUI()
        {
            BeginInspector();
            bool needWarning = false;
            for (int i = 0; !needWarning && i < targets.Length; ++i)
                needWarning = (targets[i] as CinemachineOrbitalTransposer).FollowTarget == null;
            if (needWarning)
                EditorGUILayout.HelpBox(
                    "Orbital Transposer requires a Follow target.",
                    MessageType.Warning);
            Target.m_XAxis.ValueRangeLocked
                = (Target.m_BindingMode == CinemachineTransposer.BindingMode.SimpleFollowWithWorldUp);
            DrawRemainingPropertiesInInspector();
        }

        /// Process a position drag from the user.
        /// Called "magically" by the vcam editor, so don't change the signature.
        private void OnVcamPositionDragged(Vector3 delta)
        {
            if (Target.FollowTarget != null)
            {
                Undo.RegisterCompleteObjectUndo(Target, "Camera drag");
                Quaternion targetOrientation = Target.GetReferenceOrientation(Target.VcamState.ReferenceUp);
                targetOrientation = targetOrientation * Quaternion.Euler(0, Target.m_Heading.m_Bias, 0);
                Vector3 localOffset = Quaternion.Inverse(targetOrientation) * delta;
                localOffset.x = 0;
                Target.m_FollowOffset += localOffset;
                Target.m_FollowOffset = Target.EffectiveOffset;
            }
        }

        [DrawGizmo(GizmoType.Active | GizmoType.Selected, typeof(CinemachineOrbitalTransposer))]
        static void DrawTransposerGizmos(CinemachineOrbitalTransposer target, GizmoType selectionType)
        {
            if (target.IsValid && !target.m_HideOffsetInInspector)
            {
                Color originalGizmoColour = Gizmos.color;
                Gizmos.color = CinemachineCore.Instance.IsLive(target.VirtualCamera)
                    ? CinemachineSettings.CinemachineCoreSettings.ActiveGizmoColour
                    : CinemachineSettings.CinemachineCoreSettings.InactiveGizmoColour;

                Vector3 up = target.VirtualCamera.State.ReferenceUp;
                Vector3 pos = target.FollowTargetPosition;

                Quaternion orient = target.GetReferenceOrientation(up);
                up = orient * Vector3.up;
                DrawCircleAtPointWithRadius
                    (pos + up * target.m_FollowOffset.y, orient, target.m_FollowOffset.z);

                Gizmos.color = originalGizmoColour;
            }
        }

        public static void DrawCircleAtPointWithRadius(Vector3 point, Quaternion orient, float radius)
        {
            Matrix4x4 prevMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(point, orient, radius * Vector3.one);

            const int kNumPoints = 25;
            Vector3 currPoint = Vector3.forward;
            Quaternion rot = Quaternion.AngleAxis(360f / (float)kNumPoints, Vector3.up);
            for (int i = 0; i < kNumPoints + 1; ++i)
            {
                Vector3 nextPoint = rot * currPoint;
                Gizmos.DrawLine(currPoint, nextPoint);
                currPoint = nextPoint;
            }
            Gizmos.matrix = prevMatrix;
        }
        
        internal override void OnSceneGUI()
        {
            Debug.Log(Target.GetType() + "Editor OnSceneGUI");
            DrawSceneTools(
                CinemachineSettings.CinemachineCoreSettings.k_vcamActiveToolColor,
                CinemachineSettings.CinemachineCoreSettings.k_vcamToolsColor);
        }
        
        void DrawSceneTools(Color activeColor, Color defaultColor)
        {
            var T = Target;
            if (!T.IsValid && Tools.current != Tool.Move)
            {
                return;
            }

            if (Utility.CinemachineSceneToolUtility.FollowOffsetToolIsOn)
            {
                var up = Vector3.up;
                var brain = CinemachineCore.Instance.FindPotentialTargetBrain(T.VirtualCamera);
                if (brain != null)
                    up = brain.DefaultWorldUp;
                var followTargetPosition = T.FollowTargetPosition;
                var cameraPosition = T.GetTargetCameraPosition(up);

                EditorGUI.BeginChangeCheck();
                var newPos = Handles.PositionHandle(cameraPosition, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    T.m_FollowOffset += newPos - cameraPosition;
                    
                    Undo.RecordObject(this, "Change Follow Offset Position using handle in Scene View.");
                    InspectorUtility.RepaintGameView();
                }

                var originalColor = Handles.color;
                var labelStyle = new GUIStyle();
                var handleIsUsed = GUIUtility.hotControl > 0;
                Handles.color = 
                    labelStyle.normal.textColor = handleIsUsed ? activeColor : defaultColor;
                Handles.DrawDottedLine(followTargetPosition, cameraPosition, 5f);
                Handles.Label(cameraPosition, "Follow offset " + T.m_FollowOffset.ToString("F1"), labelStyle);

                Handles.color = originalColor;
            }
        }
    }
}
