using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameBalancer.Economy
{
    public enum EconomyFlowKind
    {
        Faucet = 0,
        Sink = 1
    }

    public enum EconomyCadence
    {
        EveryDay = 0,
        EveryNDays = 1,
        OneTimeOnDay = 2
    }

    [Serializable]
    public sealed class CurrencyDefinition
    {
        public string currencyId = "Gold";
        public double startingBalance = 250d;
        public bool hasCap = false;
        public double maxCap = 1000000d;
        public double inflationAlertThreshold = 50000d;
    }

    [Serializable]
    public sealed class EconomyFlowNode
    {
        public string nodeName = "Daily Quest Reward";
        public string currencyId = "Gold";
        public EconomyFlowKind kind = EconomyFlowKind.Faucet;
        public EconomyCadence cadence = EconomyCadence.EveryDay;
        [Min(1)] public int intervalOrDay = 1;
        public bool useFormula = false;
        public double flatAmount = 100d;
        public string amountFormula = "100 + day * 15";
        public bool mandatorySink = false;
    }

    /// <summary>
    /// Reusable ScriptableObject defining currencies, faucets (income), and sinks (expenses)
    /// for multi-day or multi-session economy simulation.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyModel", menuName = "GameBalancer/Economy Model", order = 103)]
    public class EconomyModelAsset : ScriptableObject
    {
        public string modelName = "Core Progression Economy";
        public List<CurrencyDefinition> currencies = new List<CurrencyDefinition>
        {
            new CurrencyDefinition
            {
                currencyId = "Gold",
                startingBalance = 200d,
                inflationAlertThreshold = 25000d
            }
        };

        public List<EconomyFlowNode> flows = new List<EconomyFlowNode>
        {
            new EconomyFlowNode
            {
                nodeName = "Dungeon Runs",
                currencyId = "Gold",
                kind = EconomyFlowKind.Faucet,
                cadence = EconomyCadence.EveryDay,
                useFormula = true,
                amountFormula = "150 + day * 12"
            },
            new EconomyFlowNode
            {
                nodeName = "Gear Upgrades",
                currencyId = "Gold",
                kind = EconomyFlowKind.Sink,
                cadence = EconomyCadence.EveryNDays,
                intervalOrDay = 2,
                useFormula = true,
                amountFormula = "220 + day * 22",
                mandatorySink = true
            },
            new EconomyFlowNode
            {
                nodeName = "Consumables & Repairs",
                currencyId = "Gold",
                kind = EconomyFlowKind.Sink,
                cadence = EconomyCadence.EveryDay,
                flatAmount = 75d,
                mandatorySink = false
            }
        };
    }
}
