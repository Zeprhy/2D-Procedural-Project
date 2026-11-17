using TMPro;
using UnityEngine;
using System.Collections;

public class DamagePopUp2D : MonoBehaviour
{
    [SerializeField] private TextMeshPro _textMesh;

    private Vector3 _velocity;
    private Color _textColor;
    private float _disappearTimer;
    private const float DISAPPEAR_TIMER_MAX = 0.65f;

    public void Setup(float damageAmount, bool isCritical)
    {
        _textMesh.text = Mathf.RoundToInt(damageAmount).ToString();

        if (isCritical)
        {
            _textMesh.fontSize = 7;
            _textMesh.color = new Color (1f, 0.85f, 0.1f, 1f);
            _textMesh.text += "!";
            transform.localScale = Vector3.one * 1.8f;

            _velocity = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(3.5f, 5f), 0f);
        }
        else
        {
            _textMesh.fontSize = 5;
            _textMesh.color = Color.white;
            transform.localScale = Vector3.one * 1.1f;

            _velocity = new Vector3(Random.Range(-1f, 1f), Random.Range(2f, 3.5f), 0f);
        }

        _textColor = _textMesh.color;
        _disappearTimer =  DISAPPEAR_TIMER_MAX;
    }

    private void Update()
    {
        transform.position += _velocity * Time.deltaTime;
        _velocity.y -= 8f * Time.deltaTime;

        if (transform.localScale.x > 1f)
            transform.localScale -= Vector3.one * (2f * Time.deltaTime);

        _disappearTimer -= Time.deltaTime;
        if (_disappearTimer < 0)
        {
            _textColor.a -= 3 * Time.deltaTime;
            _textMesh.color = _textColor;

            if (_textColor.a <= 0)
                Destroy(gameObject);
        }
    }
}
