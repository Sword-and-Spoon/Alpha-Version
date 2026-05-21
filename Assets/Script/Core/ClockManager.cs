using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClockManager : MonoBehaviour
{
    public TextMeshProUGUI Date, Time, Season, Week;

    [Header("Time Of Day Sprite")]
    [SerializeField] private Image timeOfDayImage;
    [SerializeField] private Sprite daylightSprite;
    [SerializeField] private Sprite sunsetSprite;
    [SerializeField] private Sprite nightSprite;

    private void OnEnable()
    {
        TimeManager.OnDateTimeChanged += UpdateDateTime;
    }

    private void OnDisable()
    {
        TimeManager.OnDateTimeChanged -= UpdateDateTime;
    }

    private void Start()
    {
        if (TimeManager.Instance != null)
        {
            UpdateDateTime(TimeManager.Instance.dateTime);
        }
    }

    private void UpdateDateTime(TimeManager.DateTime dateTime)
    {
        Date.text = dateTime.DateToString();
        Time.text = dateTime.TimeToString();
        Season.text = dateTime.Season.ToString();
        Week.text = $"Wk: {dateTime.CurrentWeek}";

        UpdateTimeOfDaySprite(dateTime);
    }

    private void UpdateTimeOfDaySprite(TimeManager.DateTime dateTime)
    {
        if (timeOfDayImage == null) return;

        Sprite targetSprite;

        if (dateTime.IsNight())
        {
            targetSprite = nightSprite;
        }
        else if (dateTime.Hour >= 17 && dateTime.Hour < 19)
        {
            targetSprite = sunsetSprite;
        }
        else
        {
            targetSprite = daylightSprite;
        }

        if (targetSprite != null)
        {
            timeOfDayImage.sprite = targetSprite;
        }
    }
}
