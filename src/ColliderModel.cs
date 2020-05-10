using System;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

using Object = UnityEngine.Object;

public abstract class ColliderModel<T> : ColliderModel where T : Collider
{
    protected T Collider { get; }

    protected ColliderModel(MVRScript parent, T collider, string label)
        : base(parent, collider.Uuid(), label)
    {
        Collider = collider;
    }

    public override void CreatePreview()
    {
        var preview = DoCreatePreview();

        preview.GetComponent<Renderer>().material = MaterialHelper.GetNextMaterial();
        foreach (var c in preview.GetComponents<Collider>())
        {
            c.enabled = false;
            Object.Destroy(c);
        }

        preview.transform.SetParent(Collider.transform, false);

        Preview = preview;

        DoUpdatePreview();
        SetSelected(Selected);
    }
}

public abstract class ColliderModel : IModel
{
    private float _previewOpacity;

    private bool _selected;

    private float _selectedPreviewOpacity;
    private JSONStorableBool _xRayStorable;

    private bool _showPreview;
    protected MVRScript Parent { get; }

    public string Id { get; }
    public string Label { get; }
    public RigidbodyModel Rididbody { get; set; }

    public GameObject Preview { get; protected set; }
    public List<UIDynamic> Controls { get; private set; }

    public bool Selected
    {
        get { return _selected; }
        set
        {
            if (_selected != value)
            {
                SetSelected(value);
                _selected = value;
            }
        }
    }

    public float SelectedPreviewOpacity
    {
        get { return _selectedPreviewOpacity; }
        set
        {
            if (Mathf.Approximately(value, _selectedPreviewOpacity))
                return;

            _selectedPreviewOpacity = value;

            if (Preview != null && _selected)
            {
                var previewRenderer = Preview.GetComponent<Renderer>();
                var color = previewRenderer.material.color;
                color.a = _selectedPreviewOpacity;
                previewRenderer.material.color = color;
                previewRenderer.enabled = false;
                previewRenderer.enabled = true;
            }
        }
    }

    public float PreviewOpacity
    {
        get { return _previewOpacity; }
        set
        {
            if (Mathf.Approximately(value, _previewOpacity))
                return;

            _previewOpacity = value;

            if (Preview != null && !_selected)
            {
                var previewRenderer = Preview.GetComponent<Renderer>();
                var color = previewRenderer.material.color;
                color.a = _previewOpacity;
                previewRenderer.material.color = color;

            }
        }
    }

    public bool ShowPreview
    {
        get { return _showPreview; }
        set
        {
            _showPreview = value;

            if (_showPreview)
                CreatePreview();
            else
                DestroyPreview();
        }
    }

    private bool _xRayPreview;
    public bool XRayPreview
    {
        get { return _xRayPreview; }
        set
        {
            _xRayPreview = value;

            if (Preview != null)
            {
                var previewRenderer = Preview.GetComponent<Renderer>();
                var material = previewRenderer.material;

                if (_xRayPreview)
                {
                    material.shader = Shader.Find("Battlehub/RTGizmos/Handles");
                    material.SetFloat("_Offset", 1f);
                    material.SetFloat("_MinAlpha", 1f);
                }
                else
                {
                    material.shader = Shader.Find("Standard");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = 3000;
                }

                previewRenderer.material = material;

                if (_xRayStorable != null)
                    _xRayStorable.valNoCallback = value;
            }
        }
    }

    protected ColliderModel(MVRScript parent, string id, string label)
    {
        Parent = parent;

        Id = id;
        Label = label;
    }

    public static ColliderModel CreateTyped(MVRScript parent, Collider collider, Dictionary<string, RigidbodyModel> rigidbodies)
    {
        ColliderModel typed;

        if (collider is SphereCollider)
            typed = new SphereColliderModel(parent, (SphereCollider)collider);
        else if (collider is BoxCollider)
            typed = new BoxColliderModel(parent, (BoxCollider)collider);
        else if (collider is CapsuleCollider)
            typed = new CapsuleColliderModel(parent, (CapsuleCollider)collider);
        else
            throw new InvalidOperationException("Unsupported collider type");

        if (collider.attachedRigidbody != null)
        {
            RigidbodyModel rigidbodyModel;
            if (rigidbodies.TryGetValue(collider.attachedRigidbody.Uuid(), out rigidbodyModel))
            {
                typed.Rididbody = rigidbodyModel;
                if (rigidbodyModel.Colliders == null)
                    rigidbodyModel.Colliders = new List<ColliderModel> { typed };
                else
                    rigidbodyModel.Colliders.Add(typed);
            }
        }

        return typed;
    }

    public void CreateControls()
    {
        DestroyControls();

        var controls = new List<UIDynamic>();

        _xRayStorable = new JSONStorableBool("xRayPreview", true, (bool value) => { XRayPreview = value; });

        var xRayToggle = Parent.CreateToggle(_xRayStorable, true);
        xRayToggle.label = "XRay Preview";

        var resetUi = Parent.CreateButton("Reset Collider", true);
        resetUi.button.onClick.AddListener(ResetToInitial);

        controls.Add(xRayToggle);
        controls.Add(resetUi);
        controls.AddRange(DoCreateControls());

        Controls = controls;
    }

    public abstract IEnumerable<UIDynamic> DoCreateControls();

    public virtual void DestroyControls()
    {
        if (Controls == null)
            return;

        foreach (var adjustmentJson in Controls)
            Object.Destroy(adjustmentJson.gameObject);

        Controls.Clear();
    }

    public virtual void DestroyPreview()
    {
        if (Preview != null)
        {
            Object.Destroy(Preview);
            Preview = null;
        }
    }

    public abstract void CreatePreview();

    protected abstract GameObject DoCreatePreview();

    public void UpdatePreview()
    {
        if (_showPreview)
            DoUpdatePreview();
    }

    protected abstract void DoUpdatePreview();

    public void UpdateControls()
    {
        DoUpdateControls();
    }

    protected abstract void DoUpdateControls();

    protected virtual void SetSelected(bool value)
    {
        if (Preview != null)
        {
            var previewRenderer = Preview.GetComponent<Renderer>();
            var color = previewRenderer.material.color;
            color.a = value ? _selectedPreviewOpacity : _previewOpacity;
            previewRenderer.material.color = color;
        }

        if (value)
            CreateControls();
        else
            DestroyControls();
    }

    public void AppendJson(JSONClass parent)
    {
        parent.Add(Id, DoGetJson());
    }

    public void LoadJson(JSONClass jsonClass)
    {
        DoLoadJson(jsonClass);
        DoUpdatePreview();

        if (Selected)
        {
            DestroyControls();
            CreateControls();
        }
    }

    protected abstract void DoLoadJson(JSONClass jsonClass);

    public abstract JSONClass DoGetJson();

    public void ResetToInitial()
    {
        DoResetToInitial();
        DoUpdatePreview();

        if (Selected)
        {
            DestroyControls();
            CreateControls();
        }
    }

    protected abstract void DoResetToInitial();

    protected abstract bool DeviatesFromInitial();

    public override string ToString() => Id;
}