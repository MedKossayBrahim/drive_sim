using System;
using UnityEngine;

[Serializable]
public sealed class BoatDifficultyConfig
{
    [Header("Progression")]
    [Min(1f)] public float rampDurationSeconds = 180f;

    [Header("Boat speed")]
    [Min(0f)] public float cruiseSpeedStart = 8f;
    [Min(0f)] public float cruiseSpeedMax = 16f;
    [Min(0f)] public float boostedSpeedStart = 14f;
    [Min(0f)] public float boostedSpeedMax = 28f;

    [Header("Obstacle waves")]
    [Min(1f)] public float laneSpacing = 2.45f;
    [Min(0.2f)] public float obstacleIntervalStart = 1.9f;
    [Min(0.2f)] public float obstacleIntervalMax = 0.95f;
    [Min(1)] public int obstaclesPerWaveStart = 1;
    [Min(1)] public int obstaclesPerWaveMax = 3;
    [Min(1)] public int safeCorridorLanesStart = 3;
    [Min(1)] public int safeCorridorLanesMax = 2;
    [Min(1f)] public float obstacleWaveGapStart = 12f;
    [Min(1f)] public float obstacleWaveGapMax = 4.5f;
    [Min(1f)] public float spawnDistanceStart = 42f;
    [Min(1f)] public float spawnDistanceMax = 84f;
    [Range(0f, 1f)] public float buoyChanceStart = 0.82f;
    [Range(0f, 1f)] public float buoyChanceMax = 0.32f;
    [Range(0f, 1f)] public float rockChanceAfterBuoys = 0.34f;
    [Min(0f)] public float obstacleDriftStart = 0.45f;
    [Min(0f)] public float obstacleDriftMax = 1.35f;

    [Header("Coin trails")]
    [Min(0.2f)] public float coinIntervalStart = 2.8f;
    [Min(0.2f)] public float coinIntervalMax = 1.7f;
    [Min(0f)] public float firstCoinDelay = 1.1f;
    [Min(1)] public int coinTrailCountStart = 3;
    [Min(1)] public int coinTrailCountMax = 5;
    [Min(0.5f)] public float coinTrailSpacing = 2.15f;

    [Header("Power-up schedule")]
    [Min(1f)] public float powerUpIntervalMinimum = 12f;
    [Min(1f)] public float powerUpIntervalMaximum = 18f;
    [Min(1f)] public float powerUpGuaranteedInterval = 25f;

    [Header("Camera")]
    [Range(30f, 80f)] public float cameraFovStart = 50f;
    [Range(30f, 80f)] public float cameraFovMax = 56f;
}
