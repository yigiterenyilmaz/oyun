using System;
using System.Collections.Generic;
using UnityEngine;

public class SocialMediaManager : MonoBehaviour
{
    public static SocialMediaManager Instance { get; private set; }

    public SocialMediaPostDatabase postDatabase;

    [Header("Trend Settings")]
    public float minTrendDuration = 10f; //minimum trend süresi (saniye)
    public float maxTrendDuration = 150f; //maximum trend süresi (saniye)

    [Header("Post Settings")]
    public float minPostInterval = 1f; //postlar arası minimum süre
    public float maxPostInterval = 5f; //postlar arası maximum süre

    //runtime
    private TopicType currentTrendingTopic;
    private float currentTrendPostChance; //bu trend için post oranı (%50-%100 arası random)
    private float trendTimer = 0f;
    private float nextTrendChange;
    private float postTimer = 0f;
    private float nextPostTime;
    private HashSet<string> shownPostIds = new HashSet<string>(); //gösterilmiş postlar

    //topic ağırlıkları - skiller bunları modifiye edebilir
    private Dictionary<TopicType, float> topicWeights = new Dictionary<TopicType, float>();

    //events
    public static event Action<TopicType> OnTrendChanged;
    public static event Action<SocialMediaPost> OnNewPost;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializeTopicWeights();
    }

    private void Start()
    {
        SelectNewTrend();
        ScheduleNextPost();
    }

    private void Update()
    {
        //trend timer
        trendTimer += Time.deltaTime;
        if (trendTimer >= nextTrendChange)
        {
            SelectNewTrend();
        }

        //post timer
        postTimer += Time.deltaTime;
        if (postTimer >= nextPostTime)
        {
            TryShowNewPost();
            ScheduleNextPost();
        }
    }

    private void InitializeTopicWeights()
    {
        //tüm topic'lere başlangıç ağırlığı ver
        foreach (TopicType topic in Enum.GetValues(typeof(TopicType)))
        {
            topicWeights[topic] = 1f;
        }
    }

    public void ModifyTopicWeight(TopicType topic, float amount)
    {
        //skill'ler bu metodu çağırarak topic ağırlığını değiştirir
        if (topicWeights.ContainsKey(topic))
        {
            topicWeights[topic] += amount;
            if (topicWeights[topic] < 0f) topicWeights[topic] = 0f;
        }
    }

    public float GetTopicWeight(TopicType topic)
    {
        return topicWeights.ContainsKey(topic) ? topicWeights[topic] : 1f;
    }

    private void SelectNewTrend()
    {
        trendTimer = 0f;
        nextTrendChange = UnityEngine.Random.Range(minTrendDuration, maxTrendDuration);

        //bu trend için post oranı (%50-%100 arası random)
        currentTrendPostChance = UnityEngine.Random.Range(0.5f, 1f);

        //weighted random selection for trending topic
        float totalWeight = 0f;
        foreach (var kvp in topicWeights)
        {
            totalWeight += kvp.Value;
        }

        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var kvp in topicWeights)
        {
            cumulative += kvp.Value;
            if (randomValue <= cumulative)
            {
                currentTrendingTopic = kvp.Key;
                break;
            }
        }

        OnTrendChanged?.Invoke(currentTrendingTopic);
    }

    private void ScheduleNextPost()
    {
        postTimer = 0f;
        nextPostTime = UnityEngine.Random.Range(minPostInterval, maxPostInterval);
    }

    private void TryShowNewPost()
    {
        SocialMediaPost post = GetNextPost();
        if (post != null)
        {
            if (!post.isRepeatable)
            {
                shownPostIds.Add(post.id);
            }
            OnNewPost?.Invoke(post);
        }
    }

    private SocialMediaPost GetNextPost()
    {
        //trend topic oranı her trend için random belirlenir (%50-%100)
        bool useTrendTopic = UnityEngine.Random.value < currentTrendPostChance;

        List<SocialMediaPost> eligiblePosts = GetEligiblePosts(useTrendTopic ? currentTrendingTopic : (TopicType?)null);

        if (eligiblePosts.Count == 0)
        {
            //trend topic'te post yoksa diğerlerinden al
            eligiblePosts = GetEligiblePosts(null);
        }

        if (eligiblePosts.Count == 0)
            return null;

        //aynı topic içindeki postlar eşit olasılıkla gelir
        return eligiblePosts[UnityEngine.Random.Range(0, eligiblePosts.Count)];
    }

    private List<SocialMediaPost> GetEligiblePosts(TopicType? filterTopic)
    {
        List<SocialMediaPost> eligible = new List<SocialMediaPost>();

        foreach (SocialMediaPost post in postDatabase.allPosts)
        {
            if (IsPostEligible(post, filterTopic))
            {
                eligible.Add(post);
            }
        }

        return eligible;
    }

    private bool IsPostEligible(SocialMediaPost post, TopicType? filterTopic)
    {
        //topic filtresi
        if (filterTopic.HasValue && post.topic != filterTopic.Value)
            return false;

        //tekrar gösterilemez post zaten gösterilmiş mi
        if (!post.isRepeatable && shownPostIds.Contains(post.id))
            return false;

        return true;
    }

    public TopicType GetCurrentTrend()
    {
        return currentTrendingTopic;
    }

    public float GetTrendTimeRemaining()
    {
        return nextTrendChange - trendTimer;
    }
}
