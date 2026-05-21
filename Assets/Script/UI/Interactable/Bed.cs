using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bed : InteractableObject
{
    public static event Action<string> OnBedInteractBlocked;

    private Player player;
    public override bool CanInteract() => true;

    public override void Interact()
    {
        player = FindObjectOfType<Player>();

        if (player == null) return;

        SleepAndWakeUp();
    }

    private void SleepAndWakeUp()
    {
        if (DailyJournalRules.ShouldBlockSleep(out string message))
        {
            OnBedInteractBlocked?.Invoke(message);
            DailyJournalRules.ShowMessage(message);
            return;
        }

        int currentH = TimeManager.Instance.CurrentHour;
        string journalSummary = DailyJournalRules.BuildSleepSummary();

        StartCoroutine(ScreenFader.Instance.FadeOutInWithMessage(
            () =>
            {
                DailyJournalRules.StartNewDayAfterSleep();
                TimeManager.Instance.sleepTimer = 0;
                if (currentH < 6)
                    TimeManager.SetTime(TimeManager.Instance.newDayHour, 0);
                else
                    TimeManager.AdvanceToNextDay(TimeManager.Instance.newDayHour, 0);
                player.transform.GetComponent<PlayerHealth>().FullRestore();

                DailyJournalRules.RefreshNewDaySystemsAfterWake();
            },
            journalSummary,
            3f,
            () =>
            {
                if (SaveManager.Instance != null)
                    SaveManager.Instance.AutoSave();
            }));

    }
}
