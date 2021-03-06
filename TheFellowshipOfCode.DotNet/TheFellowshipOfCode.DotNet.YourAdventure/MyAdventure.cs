﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HTF2020.Contracts;
using HTF2020.Contracts.Enums;
using HTF2020.Contracts.Models;
using HTF2020.Contracts.Models.Adventurers;
using HTF2020.Contracts.Models.Enemies;
using HTF2020.Contracts.Models.Party;
using HTF2020.Contracts.Requests;

namespace TheFellowshipOfCode.DotNet.YourAdventure
{
    public class EnemyPoint
    {
        public EnemyGroup EnemyGroup;
        public Point Point;

        public EnemyPoint(Point point, EnemyGroup enemyGroup)
        {
            Point = point;
            EnemyGroup = enemyGroup;
        }
    }
    public class TreasurePoint
    {
        public TreasureChest TreasureChest;
        public Point Point;

        public TreasurePoint(Point point, TreasureChest treasureChest)
        {
            Point = point;
            TreasureChest = treasureChest;
        }
    }
    public class MyAdventure : IAdventure
    {
        private readonly Random _random = new Random();


        public List<Point> currentPath = null;
        public List<TreasurePoint> TreasurePoints = new List<TreasurePoint>();
        public List<Point> BlacklistPoints = new List<Point>();
        public List<EnemyPoint> enemies = new List<EnemyPoint>();

        public float[,] tilesmap;
        public Point FinishPoint;

        public Party _party = null;
        public bool hasPotions;
        public bool calculated = false;


        public Task<Party> CreateParty(CreatePartyRequest request)
        {
            var party = new Party
            {
                Name = "Monke Hakkers",
                Members = new List<PartyMember>()
            };

            for (var i = 0; i < request.MembersCount; i++)
                if (i % 2 == 0)
                    party.Members.Add(new Wizard
                    {
                        Id = i,
                        Name = $"Monke Wizard {i + 1}",
                        Constitution = 13,
                        Strength = 8,
                        Intelligence = 13
                    });
                else
                    party.Members.Add(new Fighter
                    {
                        Id = i,
                        Name = $"Monke Fighter {i + 1}",
                        Constitution = 13,
                        Strength = 13,
                        Intelligence = 8
                    });
            _party = party;
            return Task.FromResult(party);
        }

       
        public Task<Turn> PlayTurn(PlayTurnRequest request)
        {
            
            return riskBasedStrategic();

            Task<Turn> riskBasedStrategic()
            {
                


                var xLocation = request.PartyLocation.X; //X locatie partymember
                var yLocation = request.PartyLocation.Y; //Y locatie partymember
                var member = request.PartyMember;
                var map = request.Map; // De tilemap
                var possibleActions = request.PossibleActions; //Al de mogelijke acties dat het character kan doen
                var isInCombat = request.IsCombat;
                var possibleEnemies = request.PossibleTargets;

                if (possibleActions.Contains(TurnAction.DrinkPotion)) hasPotions = true;
                if(possibleActions.Contains(TurnAction.DrinkPotion) && isLowHP()) return Task.FromResult(new Turn(TurnAction.DrinkPotion));
                if (possibleActions.Contains(TurnAction.Loot))
                {
                    BlacklistPoints.Add(new Point(xLocation, yLocation));
                    return Task.FromResult(new Turn(TurnAction.Loot));
                }

                if (!calculated)
                {
                    var width = map.Tiles.GetLength(0);
                    var height = map.Tiles.GetLength(1);
                    tilesmap = new float[width, height];

                    for (var i = 0; i < width; ++i)
                    for (var j = 0; j < height; ++j)
                    {
                        var tile = map.Tiles[i, j];
                        #region WeightCalculation
                        var weight = 0.00F;
                        if (tile.TileType == TileType.Wall || tile.TerrainType == TerrainType.Water)
                            weight = 0.0f;
                        else
                            weight = 0.1f;
                        tilesmap[i, j] = weight;
                        #endregion

                        switch (tile.TileType)
                        {
                            case TileType.TreasureChest:
                                TreasurePoints.Add(new TreasurePoint(new Point(i, j), tile.TreasureChest));
                                break;
                            case TileType.Finish:
                                FinishPoint = new Point(i,j );
                                break;
                            case TileType.Enemy:
                                enemies.Add(new EnemyPoint(new Point(i, j), tile.EnemyGroup));
                                break;
                        }
                    }

                    calculated = true;
                }
                
                
                if (currentPath == null)
                {
                    var grid = new Grid(tilesmap);
                    if (TreasurePoints.Any(x => !x.TreasureChest.IsEmpty))
                    {
                        List<Point> closestPoint = null;
                        foreach (var treasure in TreasurePoints.Where(x => !x.TreasureChest.IsEmpty))
                        {
                            if (closestPoint == null)
                            {
                                closestPoint = Pathfinding.FindPath(grid, new Point(xLocation, yLocation), treasure.Point, Pathfinding.DistanceType.Manhattan);
                                continue;
                            }
                            else
                            {
                                var newPoint = Pathfinding.FindPath(grid, new Point(xLocation, yLocation), treasure.Point,
                                    Pathfinding.DistanceType.Manhattan);
                                if (closestPoint.Count > newPoint.Count)
                                {
                                    closestPoint = newPoint;
                                }
                            }
                        }
                        currentPath = closestPoint;
                        
                    }

                    //else if (!request.IsCombat&&canHandleCombat()&&!request.PossibleActions.Contains(TurnAction.Attack))
                    //{
                    //    currentPath = Pathfinding.FindPath(grid, new Point(xLocation, yLocation), enemies[0].Point, Pathfinding.DistanceType.Manhattan);
                    //}
                    //else if (request.PossibleActions.Contains(TurnAction.Attack) && canHandleCombat())
                    //{
                        
                    //    return Task.FromResult(new Turn(TurnAction.Attack));

                    //}
                    else
                    {
                        currentPath = Pathfinding.FindPath(grid, new Point(xLocation, yLocation), FinishPoint, Pathfinding.DistanceType.Manhattan);
                    }
                }

                if (currentPath != null && currentPath.Count > 0 )
                {
                    var currentStep = currentPath[0];
                    currentPath.RemoveAt(0);
                    return Task.FromResult(new Turn(GetTurnAction(xLocation, yLocation, currentStep)));
                }
                else currentPath = null;


                return Task.FromResult(new Turn(TurnAction.Pass));
            }
        }

        public bool isStrongerThan(Enemy e, PartyMember member)
        {
            switch (e.Type)
            {
                case "Sorcerer":
                    return member.Strength > 8;
                case "Heavy":
                    return member.Intelligence > 8;
                default:
                    return true;
            }
        }

        public bool canHandleCombat()
        {
            var totalHP = _party.Members.Sum(x => x.CurrentHealthPoints);
            var maxHP = _party.Members.Sum(x => x.HealthPoints);
            return (totalHP / maxHP) * 100 > 60;
        }
        public bool isLowHP()
        {
            var totalHP = _party.Members.Sum(x => x.CurrentHealthPoints);
            var maxHP = _party.Members.Sum(x => x.HealthPoints);
            return (totalHP / maxHP) * 100 > 40;
        }


        public TurnAction GetTurnAction(int oX, int oY, Point comparisonPoint)
        {
            var cX = comparisonPoint.x;
            var cY = comparisonPoint.y;
            if (oX - cX > 0) return TurnAction.WalkWest;
            else if (oX - cX < 0) return TurnAction.WalkEast;
            else if (oY - cY > 0) return TurnAction.WalkNorth;
            else if (oY - cY < 0) return TurnAction.WalkSouth;
            else return TurnAction.Pass;
        }

        public float EnemyGroupDifficulty(EnemyGroup enemies, Party party)
        {
            var totalCost = 0.00F;
            foreach (var enemy in enemies.Enemies)
                if (enemy.GetType() == typeof(Goblin))
                    totalCost += 2;
                else if (enemy.GetType() == typeof(Sorcerer) || enemy.GetType() == typeof(Heavy)) totalCost += 3;

            if (hasPotions) totalCost -= 2;

            return totalCost;
        }
    }
}