using UnityEngine;

public class KeyboardControl : MonoBehaviour
{
    [Header("Vitesse de déplacement (unités/seconde)")]
    public float moveSpeed = 5f;

    [Header("Axes de contrôle")]
    public bool allowVertical = true;
    public bool allowHorizontal = true;

    void Update()
    {
        // Récupération des entrées clavier
        float moveX = allowHorizontal ? Input.GetAxis("Horizontal") : 0f;
        float moveZ = allowVertical ? Input.GetAxis("Vertical") : 0f;

        // Calcul du vecteur de déplacement normalisé
        Vector3 movement = new Vector3(moveX, 0f, moveZ);

        if (movement.sqrMagnitude > 0.001f)
        {
            // Normaliser pour une vitesse constante diagonale
            movement.Normalize();

            // Calcul de la distance à parcourir ce frame
            float step = moveSpeed * Time.deltaTime;

            // Application du déplacement
            transform.Translate(movement * step, Space.World);

            // Orientation dans la direction du mouvement
            transform.forward = movement;
        }
    }
}