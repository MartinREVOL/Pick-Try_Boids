using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implementation of http://www.csc.kth.se/utbildning/kth/kurser/DD143X/dkand13/Group9Petter/report/Martin.Barksten.David.Rydberg.report.pdf
/// </summary>
public class SheepBoid : MonoBehaviour
{
    /// <summary>
    /// Vector created by applying all the herding rules
    /// </summary>
    Vector3 targetVelocity;

    /// <summary>
    /// Actual velocity of the sheep
    /// </summary>
    Vector3 velocity;

    Transform predator;
    
    // === Herding parameters ===

// Distance à laquelle le mouton commence à fuir le prédateur
    [SerializeField] float flightZoneRadius = 3f;

// --- Cohesion rule weights ---
    [SerializeField] float weightCohesionBase = 1.5f;
    [SerializeField] float weightCohesionFear = 2f;

// --- Separation rule weights ---
    [SerializeField] float weightSeparationBase = 2f;
    [SerializeField] float weightSeparationFear = 3f;

// --- Alignment rule parameters ---
    [SerializeField] float alignementZoneRadius = 3f;
    [SerializeField] float weightAlignmentBase = 0.1f;
    [SerializeField] float weightAlignmentFear = 1f;

// --- Escape rule ---
    [SerializeField] float weightEscape = 8f;
    
// --- Cohesion comfort/zone ---
    [SerializeField] float cohesionZoneRadius = 6f;       // au-delà de cette portée on prend les voisins pour le centre
    [SerializeField] float cohesionComfortDistance = 2f;  // en-dessous de cette distance moyenne, cohésion = 0

    [SerializeField] float maxSpeedCalm = 1f;
    [SerializeField] float maxSpeedFear = 4f;


    void Start()
    {
        predator = GameObject.FindGameObjectWithTag("Predator").transform;
    }

    /// <summary>
    /// 3.1
    /// Sigmoid function, used for impact of second multiplier
    /// </summary>
    /// <param name="x">Distance to the predator</param>
    /// <returns>Weight of the rule</returns>
    // x = distance au prédateur
    float P(float x)
    {
        const float k = 0.3f; // pente de transition
        // P ~ 1 quand x << flightZoneRadius, P ~ 0 quand x >> flightZoneRadius
        return 1f / (1f + Mathf.Exp((x - flightZoneRadius) / k));
    }

    /// <summary>
    /// 3.2
    /// Combine the two weights affecting the rules
    /// </summary>
    /// <param name="mult1">first multiplier</param>
    /// <param name="mult2">second multipler</param>
    /// <param name="x">distance to the predator</param>
    /// <returns>Combined weights</returns>
    float CombineWeight(float mult1, float mult2, float x)
    {
        // Combine the two multipliers, where P(x) controls the influence of the second multiplier
        return mult1 * (1f - P(x)) + mult2 * P(x);
    }

    /// <summary>
    /// 3.3
    /// In two of the rules, Separation and Escape, nearby objects are prioritized higher than
    /// those further away. This prioritization is described by an inverse square function
    /// </summary>
    /// <param name="x">Distance to the predator</param>
    /// <param name="s">Softness factor</param>
    /// <returns></returns>
    float Inv(float x, float s)
    {
        // Inverse-square attenuation with softness to avoid extreme values near zero
        return 1f / (x * x + s);
    }

    /// <summary>
    /// 3.4
    /// The Cohesion rule is calculated for each sheep s with position sp.
    /// </summary>
    /// <returns>coh(s) the cohesion vector</returns>
    Vector3 RuleCohesion()
    {
        SheepBoid[] sheeps = SheepHerd.Instance.sheeps;
        if (sheeps == null || sheeps.Length == 0)
            return Vector3.zero;

        Vector3 sumPos = Vector3.zero;
        float sumDist = 0f;
        int count = 0;

        // On ne considère que les voisins dans un rayon de cohésion
        foreach (var sheep in sheeps)
        {
            if (sheep == this) continue;

            float d = Vector3.Distance(transform.position, sheep.transform.position);
            if (d <= cohesionZoneRadius)
            {
                sumPos += sheep.transform.position;
                sumDist += d;
                count++;
            }
        }

        if (count == 0)
            return Vector3.zero;

        Vector3 center = sumPos / count;
        Vector3 dir = center - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 1e-6f)
            return Vector3.zero;

        dir.Normalize();

        // Facteur de cohésion : 0 si troupeau déjà "confort", 1 si très dispersé
        float avgDist = sumDist / count;
        float factor = Mathf.InverseLerp(cohesionComfortDistance, cohesionZoneRadius, avgDist);
        // avgDist <= comfort  -> factor = 0 (pas d’attraction)
        // avgDist >= zone     -> factor = 1 (attraction max)

        Vector3 result = dir * factor;

        // Debug optionnel pour visualiser la cohésion
        // Debug.DrawRay(transform.position, result * 2f, Color.cyan);

        return result;
    }


    /// <summary>
    /// 3.5
    /// The Separation rule
    /// </summary>
    /// <returns>sep(s) the separation vector</returns>
    Vector3 RuleSeparation()
    {
        SheepBoid[] sheeps = SheepHerd.Instance.sheeps;
        if (sheeps == null || sheeps.Length <= 1)
            return Vector3.zero;

        Vector3 separation = Vector3.zero;
        int count = 0;
        float separationZoneRadius = 3f; // rayon d’influence

        foreach (var sheep in sheeps)
        {
            if (sheep == this)
                continue;

            Vector3 offset = transform.position - sheep.transform.position;
            float distance = offset.magnitude;

            // Considère seulement les voisins proches
            if (distance > 0f && distance < separationZoneRadius)
            {
                float strength = Inv(distance, 0.1f) * 5f; // répulsion forte à courte portée
                Vector3 away = offset.normalized * strength;

                separation += away;
                count++;
            }
        }

        if (count == 0)
            return Vector3.zero;

        // Debug visuel pour vérifier que la séparation agit
        Debug.DrawRay(transform.position, separation * 2f, Color.red);


        return separation / count; // pas de normalisation complète
    }


    /// <summary>
    /// 3.6
    /// The Alignment rule
    /// </summary>
    /// <returns>ali(s) the alignement vector</returns>
    Vector3 RuleAlignment()
    {
        SheepBoid[] sheeps = SheepHerd.Instance.sheeps;

        if (sheeps == null || sheeps.Length == 0)
            return Vector3.zero;

        Vector3 avgVelocity = Vector3.zero;
        int count = 0;

        foreach (var sheep in sheeps)
        {
            if (sheep == this)
                continue;

            float distance = Vector3.Distance(transform.position, sheep.transform.position);

            // Considère seulement les voisins dans le rayon d’alignement
            if (distance <= alignementZoneRadius)
            {
                avgVelocity += sheep.velocity; // s’aligne sur la vitesse des voisins
                count++;
            }
        }

        // S’il n’y a pas de voisins proches, aucun alignement
        if (count == 0)
            return Vector3.zero;

        // Moyenne des directions
        avgVelocity /= count;

        // Normaliser pour ne garder que la direction
        if (avgVelocity != Vector3.zero)
            avgVelocity.Normalize();

        return avgVelocity;
    }

    /// <summary>
    /// 3.8
    /// The Escape rule
    /// </summary>
    /// <returns>esc(s) the escape vector</returns>
    Vector3 RuleEscape()
    {
        if (predator == null)
            return Vector3.zero;

        // Vecteur du prédateur vers le mouton
        Vector3 offset = transform.position - predator.position;
        float distance = offset.magnitude;

        if (distance <= 0f)
            return Vector3.zero;

        // Direction opposée au prédateur, pondérée par l’inverse carré (softness = 2)
        Vector3 escape = offset.normalized * Inv(distance, 2f);

        // Normaliser le vecteur final pour garder uniquement la direction
        if (escape != Vector3.zero)
            escape.Normalize();

        return escape;
    }

    /// <summary>
    /// 3.9
    /// Get the intended velocity of the sheep by applying all the herding rules
    /// </summary>
    /// <returns>The resulting vector of all the rules</returns>
    Vector3 ApplyRules()
    {
        // Calcul de la distance au prédateur
        float distanceToPredator = (transform.position - predator.position).magnitude;

        // Calcul des vecteurs de chaque règle
        Vector3 coh = RuleCohesion();
        Vector3 sep = RuleSeparation();
        Vector3 ali = RuleAlignment();
        Vector3 esc = RuleEscape();
        Vector3 pen = Pen.Instance.RuleEnclosed(transform.position); // règle d’encloisonnement

        // Calcul des poids combinés en fonction de la peur (distance au prédateur)
        float wC = CombineWeight(weightCohesionBase, weightCohesionFear, distanceToPredator);
        float wS = CombineWeight(weightSeparationFear, weightSeparationBase, distanceToPredator);
        float wA = CombineWeight(weightAlignmentBase, weightAlignmentFear, distanceToPredator);
        float wE = weightEscape * P(distanceToPredator);
        float wPen = 3f; // poids fixe de la règle d’encloisonnement

        // Combinaison des règles selon leurs poids
        Vector3 v =
            (coh * wC) +
            (sep * wS) +
            (ali * wA) +
            (esc * wE) +
            (pen * wPen);

        // Si le vecteur est trop petit, on ignore le mouvement
        if (v.magnitude < 0.001f)
            return Vector3.zero;
        

        return v.normalized;
    }

    void Update()
    {
        targetVelocity = ApplyRules();
    }

    #region Move

    /// <summary>
    /// Move the sheep based on the result of the rules
    /// </summary>
    void Move()
    {
        //Velocity under which the sheep do not move
        float minVelocity = 0.1f;

        float distanceToPredator = (transform.position - predator.position).magnitude;

        // Peur ∈ [0..1] : proche -> 1, loin -> 0 (ta P déjà corrigée)
        float fear = P(distanceToPredator);

        // Vitesse max contrôlée UNIQUEMENT par la peur (plus propre)
        float currentMaxVelocity = Mathf.Lerp(maxSpeedCalm, maxSpeedFear, fear);

        // targetVelocity est maintenant une DIRECTION (ApplyRules normalise)
        Vector3 desired = targetVelocity * currentMaxVelocity;

        // Ignore small velocities
        if (desired.magnitude < minVelocity)
            desired = Vector3.zero;

        Debug.DrawRay(transform.position, desired, Color.blue);

        velocity = desired;
        velocity.y = 0f;

        transform.Translate(velocity * Time.deltaTime, Space.World);
    }


    void LateUpdate()
    {
        Move();
    }
    #endregion
}
