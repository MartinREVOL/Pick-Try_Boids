using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class SheepZone : MonoBehaviour
{
    [Header("Filtrage")]
    [Tooltip("Détecter par composant SheepBoid plutôt que par tag.")]
    public bool detectByComponent = true;

    [Tooltip("Si detectByComponent = false, on utilisera ce tag pour détecter les moutons.")]
    public string sheepTag = "Sheep";

    [Header("Seuils de comptage")]
    [Min(1)] public int maxCount = 10;       // au-delà, la couleur reste verte
    public bool clampOverMaxToGreen = true;  // si false: continue d’interpoler au-delà (reste vert de toute façon)

    [Header("Couleurs")]
    public Color emptyColor = Color.red;
    public Color fullColor = Color.green;

    [Header("Cible de rendu (sinon cherche Renderer sur ce GO)")]
    public Renderer targetRenderer;

    [Header("Événements")]
    public UnityEvent<int> onCountChanged;

    // --- Runtime ---
    readonly HashSet<SheepBoid> _inside = new HashSet<SheepBoid>();
    Collider _col;
    Renderer _renderer;
    MaterialPropertyBlock _mpb;

    public int Count => _inside.Count;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    void Awake()
    {
        _col = GetComponent<Collider>();
        if (_col) _col.isTrigger = true;

        _renderer = targetRenderer != null ? targetRenderer : GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();

        UpdateVisuals();
    }

    void OnEnable()
    {
        _inside.Clear();
        UpdateVisuals();
    }

    void OnDisable()
    {
        _inside.Clear();
        UpdateVisuals();
    }

    void OnTriggerEnter(Collider other)
    {
        if (TryGetSheep(other, out SheepBoid sheep))
        {
            if (_inside.Add(sheep))
                UpdateVisuals();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (TryGetSheep(other, out SheepBoid sheep))
        {
            if (_inside.Remove(sheep))
                UpdateVisuals();
        }
    }

    // Sécurise les cas où un mouton disparaît à l'intérieur (destroy/disable)
    void OnTriggerStay(Collider other)
    {
        if (TryGetSheep(other, out SheepBoid sheep))
        {
            // Rien de spécial ici, le HashSet évite les doublons.
            // Mais on pourrait vérifier sheep.enabled, etc.
        }
    }

    bool TryGetSheep(Collider other, out SheepBoid sheep)
    {
        sheep = null;

        if (detectByComponent)
        {
            sheep = other.GetComponentInParent<SheepBoid>();
            return sheep != null;
        }
        else
        {
            if (other.CompareTag(sheepTag))
            {
                sheep = other.GetComponentInParent<SheepBoid>();
                // même si SheepBoid est absent, on compte quand même par tag :
                if (sheep == null) sheep = DummySheep(other);
                return true;
            }
        }

        return false;
    }

    // Permet de compter par Tag même sans composant SheepBoid (on évite les null dans le HashSet)
    SheepBoid DummySheep(Collider col)
    {
        // Crée un petit wrapper non-Mono pour la clé ? Non possible.
        // Ici, on préfère chercher un SheepBoid ; si absent, on ne met rien dans le HashSet
        // pour éviter des fuites. Alternative: switch sur detectByComponent = true.
        return null;
    }

    void UpdateVisuals()
    {
        float t = 0f;
        if (maxCount > 0)
        {
            t = Mathf.Clamp01((float)Count / maxCount);
        }

        // Couleur interpolée
        Color c = Color.Lerp(emptyColor, fullColor, t);

        // Applique la couleur via MPB (évite d'instancier le matériel)
        if (_renderer != null)
        {
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", c);

            // Si le shader n’utilise pas _Color, tenter base color des HDRP/URP :
            if (_renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty("_BaseColor"))
                _mpb.SetColor("_BaseColor", c);

            _renderer.SetPropertyBlock(_mpb);
        }

        onCountChanged?.Invoke(Count);
    }

    // Gizmo pour voir la zone facilement
    void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (!col) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);

        if (col is BoxCollider bc)
        {
            Matrix4x4 m = Matrix4x4.TRS(bc.transform.position, bc.transform.rotation, bc.transform.lossyScale);
            Gizmos.matrix = m;
            Gizmos.DrawCube(bc.center, bc.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (col is SphereCollider sc)
        {
            Gizmos.DrawSphere(sc.transform.TransformPoint(sc.center), sc.radius * Mathf.Max(sc.transform.lossyScale.x, Mathf.Max(sc.transform.lossyScale.y, sc.transform.lossyScale.z)));
        }
    }
}
