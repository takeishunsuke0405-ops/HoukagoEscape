/// <summary>校門の外に出たら、エンディングを始める</summary>
public class EscapeTrigger : PlayerZone
{
    [UnityEngine.SerializeField] EndingSequence ending;

    public void Setup(EndingSequence endingSequence) => ending = endingSequence;

    protected override void OnPlayerEnter() => ending.Play();
}
