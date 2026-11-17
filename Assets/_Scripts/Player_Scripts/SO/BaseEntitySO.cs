using UnityEngine;

[CreateAssetMenu(fileName = "New_EntityData", menuName = "Entity/Entity Data")]
public class BaseEntitySO : ScriptableObject
{
    [Header("Identitas")]
    [SerializeField] private string _entityID;
    [SerializeField] private string _entityName;

    [Header("Statistik Nyawa")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private bool _isImmortal = false; // can't die? (true untuk dummy latihan abadi)//

    public string EntityID => _entityID;
    public string EntityName => _entityName;
    public float MaxHealth => _maxHealth;
    public bool IsImmortal => _isImmortal;
}