public interface IWeapon
{
    void Attack();
    void HitFrame();
    bool CanAttack();
    void ResetIsAttacking();
}
