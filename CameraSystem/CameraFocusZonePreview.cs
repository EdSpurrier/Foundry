#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Foundry.CameraSystem
{
    // Editor-only: while a CameraFocusZone is selected, the Scene view shows
    //  - the zone's camera frame on the play plane (Z 0) - what the screen will show there,
    //  - handles: drag the centre to move the view (Hold Still) or the shift (Follow), the corner to change the distance,
    //    and (Follow) the stand-in player to see how the view moves with it,
    //  - a live preview of the game camera's view in the bottom-right corner, with a button to show it in the Game
    //    view too (the main camera moves there while the zone stays selected, and goes back when it's deselected).
    [InitializeOnLoad]
    internal static class CameraFocusZonePreview
    {
        private const int PreviewWidth = 384;
        private static readonly Color FrameColor = new(1f, 0.78f, 0.2f, 1f);
        private static readonly Color PlayerColor = new(0.17f, 0.82f, 0.7f, 1f);

        private static UnityEngine.Camera _previewCamera;
        private static RenderTexture _texture;
        private static bool _inGameView;
        private static Transform _movedCamera;
        private static Vector3 _movedCameraHome;
        private static (UnityEngine.Camera main, Vector3 pos, int w, int h) _request;
        private static bool _hasRequest;
        private static Vector3 _renderedAt;
        private static double _renderedTime;

        static CameraFocusZonePreview()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += Update;
            Selection.selectionChanged += () => { if (Selected() == null) StopGameView(); };
            EditorApplication.playModeStateChanged += _ => StopGameView();
            AssemblyReloadEvents.beforeAssemblyReload += () => { StopGameView(); Cleanup(); };
        }

        private static CameraFocusZone Selected()
        {
            GameObject go = Selection.activeGameObject;
            return go != null ? go.GetComponent<CameraFocusZone>() : null;
        }

        private static void OnSceneGUI(SceneView view)
        {
            CameraFocusZone zone = Selected();
            if (zone == null || EditorApplication.isPlaying)
                return;

            UnityEngine.Camera main = UnityEngine.Camera.main;
            float fov = main != null ? main.fieldOfView : 60f;
            float aspect = main != null && main.aspect > 0.1f ? main.aspect : 16f / 9f;
            float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            Vector3 player = Flat(zone.PreviewPlayerWorld);
            Vector3 camPos = zone.CameraPositionFor(player);
            Vector3 centre = Flat(camPos);
            float halfH = zone.Distance * tan;
            float halfW = halfH * aspect;

            DrawFrame(centre, halfW, halfH, zone);

            if (zone.Mode == CameraFocusZone.FramingMode.Follow)
                DrawPlayerHandle(zone, player);

            // centre handle: moves the view (Hold Still) or the shift from the player (Follow)
            EditorGUI.BeginChangeCheck();
            Handles.color = FrameColor;
            Vector3 newCentre = Slide(centre, 0.12f, Handles.RectangleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(zone, "Move Camera View");
                if (zone.Mode == CameraFocusZone.FramingMode.HoldStill)
                    zone.SetViewCentreWorld(Flat(newCentre));
                else
                    zone.SetFollowShift(new Vector2(newCentre.x - player.x, newCentre.y - player.y));
            }

            // corner handle: how far back the camera is
            Vector3 corner = centre + new Vector3(halfW, halfH, 0f);
            EditorGUI.BeginChangeCheck();
            Vector3 newCorner = Slide(corner, 0.1f, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(zone, "Camera Distance");
                float dx = Mathf.Abs(newCorner.x - centre.x), dy = Mathf.Abs(newCorner.y - centre.y);
                zone.SetDistance(Mathf.Max(dx / (tan * aspect), dy / tan));
            }

            if (_inGameView && main != null)
                MoveGameCamera(main.transform, camPos);

            if (zone.ShowPreviewWindow)
                DrawPreviewWindow(view, main, camPos, aspect);
        }

        private static void DrawFrame(Vector3 c, float halfW, float halfH, CameraFocusZone zone)
        {
            Vector3[] rect =
            {
                c + new Vector3(-halfW, -halfH, 0f), c + new Vector3(-halfW, halfH, 0f),
                c + new Vector3(halfW, halfH, 0f), c + new Vector3(halfW, -halfH, 0f),
            };
            Handles.DrawSolidRectangleWithOutline(rect, new Color(FrameColor.r, FrameColor.g, FrameColor.b, 0.04f), FrameColor);
            // thirds, to help place things
            Handles.color = new Color(FrameColor.r, FrameColor.g, FrameColor.b, 0.25f);
            for (int i = 1; i < 3; i++)
            {
                float x = -halfW + halfW * 2f * i / 3f, y = -halfH + halfH * 2f * i / 3f;
                Handles.DrawDottedLine(c + new Vector3(x, -halfH, 0f), c + new Vector3(x, halfH, 0f), 4f);
                Handles.DrawDottedLine(c + new Vector3(-halfW, y, 0f), c + new Vector3(halfW, y, 0f), 4f);
            }
            string label = zone.Mode == CameraFocusZone.FramingMode.HoldStill
                ? $"{zone.name}\nHold still, {zone.Distance:F1} m back"
                : $"{zone.name}\nFollow, {zone.Distance:F1} m back, shift ({zone.FollowShift.x:F1}, {zone.FollowShift.y:F1})";
            Handles.Label(c + new Vector3(-halfW, halfH, 0f), label, EditorStyles.whiteBoldLabel);
        }

        private static void DrawPlayerHandle(CameraFocusZone zone, Vector3 feet)
        {
            Handles.color = PlayerColor;
            Vector3[] body =
            {
                feet + new Vector3(-0.45f, 0f, 0f), feet + new Vector3(-0.45f, 1.3f, 0f),
                feet + new Vector3(0.45f, 1.3f, 0f), feet + new Vector3(0.45f, 0f, 0f),
            };
            Handles.DrawSolidRectangleWithOutline(body, new Color(PlayerColor.r, PlayerColor.g, PlayerColor.b, 0.35f), PlayerColor);
            Handles.Label(feet + new Vector3(0.6f, 1.3f, 0f), "Player (preview)", EditorStyles.miniBoldLabel);

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Slide(feet + Vector3.up * 0.65f, 0.1f, Handles.CircleHandleCap) - Vector3.up * 0.65f;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(zone, "Move Preview Player");
                zone.SetPreviewPlayerWorld(Flat(moved));
            }
        }

        private static Vector3 Slide(Vector3 at, float size, Handles.CapFunction cap)
        {
            float s = HandleUtility.GetHandleSize(at) * size;
            return Handles.Slider2D(at, Vector3.back, Vector3.right, Vector3.up, s, cap, 0f);
        }

        private static void DrawPreviewWindow(SceneView view, UnityEngine.Camera main, Vector3 camPos, float aspect)
        {
            int w = PreviewWidth, h = Mathf.RoundToInt(PreviewWidth / aspect);
            Rect area = new(view.position.width - w - 12f, view.position.height - h - 52f, w, h);

            // the render itself happens in EditorApplication.update: rendering inside the Scene view's GUI pass would
            // draw every gizmo into the preview
            if (Event.current.type == EventType.Repaint && main != null)
            {
                _request = (main, camPos, w, h);
                _hasRequest = true;
            }

            Handles.BeginGUI();
            GUI.Box(new Rect(area.x - 4f, area.y - 22f, area.width + 8f, area.height + 26f), GUIContent.none);
            GUI.Label(new Rect(area.x, area.y - 20f, 200f, 18f), "Game camera preview", EditorStyles.boldLabel);
            bool show = GUI.Toggle(new Rect(area.xMax - 130f, area.y - 20f, 130f, 18f), _inGameView, "Show in Game view", EditorStyles.miniButton);
            if (show != _inGameView)
            {
                if (show) _inGameView = true; else StopGameView();
            }
            if (_texture != null)
                GUI.DrawTexture(area, _texture, ScaleMode.StretchToFill, false);
            else
                GUI.Label(area, "No main camera to preview with");
            Handles.EndGUI();
        }

        private static void Update()
        {
            if (_movedCamera != null && (Selected() == null || EditorApplication.isPlayingOrWillChangePlaymode))
                StopGameView();
            if (!_hasRequest || EditorApplication.isPlaying || Selected() == null || _request.main == null)
                return;
            bool moved = _request.pos != _renderedAt || _texture == null || _texture.width != _request.w || _texture.height != _request.h;
            if (!moved && EditorApplication.timeSinceStartup < _renderedTime + 0.5)
                return;
            Render(_request.main, _request.pos, _request.w, _request.h);
            _renderedAt = _request.pos;
            _renderedTime = EditorApplication.timeSinceStartup;
            if (moved)
                SceneView.RepaintAll();
        }

        private static void Render(UnityEngine.Camera main, Vector3 camPos, int w, int h)
        {
            if (_previewCamera == null)
            {
                var go = new GameObject("Camera Focus Zone Preview") { hideFlags = HideFlags.HideAndDontSave };
                _previewCamera = go.AddComponent<UnityEngine.Camera>();
                _previewCamera.enabled = false;
            }
            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                if (_texture != null) Object.DestroyImmediate(_texture);
                _texture = new RenderTexture(w, h, 24) { hideFlags = HideFlags.HideAndDontSave };
            }
            _previewCamera.CopyFrom(main);
            _previewCamera.enabled = false;
            _previewCamera.transform.SetPositionAndRotation(camPos, main.transform.rotation);
            _previewCamera.targetTexture = _texture;
            _previewCamera.Render();
        }

        private static void MoveGameCamera(Transform cam, Vector3 position)
        {
            if (_movedCamera == null)
            {
                _movedCamera = cam;
                _movedCameraHome = cam.position;
            }
            if (_movedCamera.position != position)
            {
                _movedCamera.position = position;
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        private static void StopGameView()
        {
            _inGameView = false;
            if (_movedCamera != null)
                _movedCamera.position = _movedCameraHome;
            _movedCamera = null;
        }

        private static void Cleanup()
        {
            if (_previewCamera != null) Object.DestroyImmediate(_previewCamera.gameObject);
            if (_texture != null) Object.DestroyImmediate(_texture);
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, v.y, 0f);
    }
}
#endif
