using UnityEngine;

namespace BrineTime
{
    /// <summary>
    /// How other players appear: a chunky angler with a name tag, rod, bobber and line. Parented to the
    /// NetPlayer, whose transform FishNet interpolates; the bobber is smoothed here between packets.
    /// </summary>
    public class AvatarView : MonoBehaviour
    {
        NetPlayer _owner;
        Transform _head, _rodTip, _bobber;
        LineRenderer _line;
        TextMesh _label;
        MeshRenderer _bodyRenderer, _hatRenderer;
        Vector3 _bobberTarget;
        bool _hasBobber, _bobberInit;
        float _tension, _pitch;
        GameObject _trophy;
        float _trophyTimer, _stepDistance;
        Vector3 _lastPos;

        public static AvatarView Create(NetPlayer owner)
        {
            var go = new GameObject("Avatar");
            go.transform.SetParent(owner.transform, false);
            var a = go.AddComponent<AvatarView>();
            a._owner = owner;
            a.Build();
            return a;
        }

        void Build()
        {
            var body = Gfx.Sphere(transform, new Vector3(0, 0.75f, 0), 0.35f, Color.white);
            body.transform.localScale = new Vector3(0.7f, 1.4f, 0.7f);
            _bodyRenderer = body.GetComponent<MeshRenderer>();
            _head = Gfx.Child(transform, "Head").transform;
            _head.localPosition = new Vector3(0, 1.6f, 0);
            Gfx.Sphere(_head, Vector3.zero, 0.26f, new Color(0.96f, 0.8f, 0.66f));
            var hat = Gfx.Cylinder(_head, 0.2f, 0.34f, 0.14f, new Vector3(0, 0.24f, 0), Color.white, false, 16);
            _hatRenderer = hat.GetComponent<MeshRenderer>();
            foreach (float side in new[] { -1f, 1f })
                Gfx.Sphere(_head, new Vector3(side * 0.1f, 0.04f, 0.23f), 0.04f, new Color(0.05f, 0.05f, 0.05f));
            var rod = Gfx.Cylinder(_head, 0.008f, 0.02f, 1.4f, new Vector3(0.35f, -0.2f, 0.55f), new Color(0.45f, 0.3f, 0.15f), false, 8);
            rod.transform.localRotation = Quaternion.Euler(55f, 0, 0);
            _rodTip = Gfx.Child(_head, "RodTip").transform;
            _rodTip.localPosition = new Vector3(0.35f, 0.37f, 0.95f);

            var labelGo = Gfx.Child(transform, "Name");
            labelGo.transform.localPosition = new Vector3(0, 2.25f, 0);
            _label = labelGo.AddComponent<TextMesh>();
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.fontSize = 64;
            _label.characterSize = 0.02f;
            _label.font = Ui.Font;
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = Ui.Font.material;
            labelGo.AddComponent<Billboard>();

            var bob = Gfx.Sphere(null, Vector3.zero, 0.11f, new Color(0.9f, 0.15f, 0.1f));
            bob.name = "AvatarBobber";
            _bobber = bob.transform;
            bob.SetActive(false);

            var lineGo = new GameObject("AvatarLine");
            lineGo.transform.SetParent(transform, false);
            _line = lineGo.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 13;
            _line.startWidth = _line.endWidth = 0.012f;
            _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.startColor = _line.endColor = new Color(0.95f, 0.95f, 0.9f);
            _line.enabled = false;
            _lastPos = transform.position;
            SetIdentity(_owner.DisplayName, _owner.BodyColor);
        }

        public void SetIdentity(string displayName, Color color)
        {
            if (_label == null) return;
            _label.text = displayName;
            _label.color = Color.Lerp(color, Color.white, 0.4f);
            _bodyRenderer.sharedMaterial = Gfx.Mat(color);
            _hatRenderer.sharedMaterial = Gfx.Mat(color * 0.65f + new Color(0, 0, 0, 0.35f));
        }

        public void ApplyState(float pitch, bool hasBobber, Vector3 bobber, float tension)
        {
            _pitch = pitch;
            _hasBobber = hasBobber;
            _bobberTarget = bobber;
            _tension = tension;
            if (hasBobber && !_bobberInit)
            {
                _bobber.position = bobber;
                _bobberInit = true;
            }
            if (!hasBobber) _bobberInit = false;
        }

        public void ShowCatch(string id, float size, bool golden)
        {
            if (_trophy != null) Destroy(_trophy);
            _trophy = FishModel.Build(id, 1f, golden);
            _trophy.transform.SetParent(transform, false);
            float length = Data.Creatures[id].Length * size;
            _trophy.transform.localScale = Vector3.one * Mathf.Clamp(0.7f / Mathf.Max(length, 0.01f), 0.4f, 3f) * size;
            _trophy.transform.localPosition = new Vector3(0, 2.9f, 0);
            _trophyTimer = 4f;
        }

        void Update()
        {
            _head.localRotation = Quaternion.Slerp(_head.localRotation, Quaternion.Euler(_pitch, 0, 0), Time.deltaTime * 12f);

            // footsteps so you hear friends walking by
            var flat = transform.position - _lastPos;
            flat.y = 0;
            if (flat.magnitude > 5f) _lastPos = transform.position;  // teleported (ferry)
            else
            {
                _stepDistance += flat.magnitude;
                _lastPos = transform.position;
                if (_stepDistance > 1.9f)
                {
                    _stepDistance = 0;
                    Sfx.PlayAt("step_wood", transform.position, Random.Range(0.9f, 1.1f), -6f);
                }
            }

            _bobber.gameObject.SetActive(_hasBobber);
            _line.enabled = _hasBobber;
            if (_hasBobber)
            {
                _bobber.position = Vector3.Lerp(_bobber.position, _bobberTarget, Time.deltaTime * 12f);
                Vector3 a = _rodTip.position, b = _bobber.position;
                float sag = Mathf.Min(Vector3.Distance(a, b) * 0.06f, 1.2f) * (1f - Mathf.Clamp01(_tension * 2f));
                for (int i = 0; i < 13; i++)
                {
                    float t = i / 12f;
                    var p = Vector3.Lerp(a, b, t);
                    p.y -= sag * 4f * t * (1f - t);
                    _line.SetPosition(i, p);
                }
            }

            if (_trophy != null)
            {
                _trophyTimer -= Time.deltaTime;
                _trophy.transform.Rotate(0, 115f * Time.deltaTime, 0);
                if (_trophyTimer <= 0) { Destroy(_trophy); _trophy = null; }
            }
        }

        void OnDestroy()
        {
            if (_bobber != null) Destroy(_bobber.gameObject);
        }
    }
}
