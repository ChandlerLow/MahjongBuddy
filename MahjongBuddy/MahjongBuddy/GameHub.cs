﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using MahjongBuddy.Models;
using System.Threading.Tasks;
using MahjongBuddy.Extensions;
using System.Collections;
namespace MahjongBuddy
{
    public class GameHub : Hub
    {
        public async void Join(string userName, string groupName)
        {
            
            var player = GameState.Instance.GetPlayer(userName);
            if (player != null)
            {
                Clients.Caller.playerExists();
            }


            player = GameState.Instance.CreatePlayer(userName, groupName);
            player.ConnectionId = Context.ConnectionId;
            Clients.Caller.name = player.Name;
            Clients.Caller.hash = player.Hash;
            Clients.Caller.id = player.Id;

            await Groups.Add(Context.ConnectionId, groupName);
            Clients.OthersInGroup(groupName).notifyUserInGroup(userName + " joined.");                        

            StartGame(player);
        }

        private bool StartGame(Player player)
        {
            if (player != null)
            {
                Player player2;
                Player player3;
                Player player4;

                var pepInGroup = GameState.Instance.Players.Where(p => p.Value.Group == player.Group && p.Value.Id != player.Id);

                if (pepInGroup.Count() == 3)
                {
                    Clients.Group(player.Group).gameStarted();

                    var arrayOfPep = pepInGroup.ToArray();
                    player2 = arrayOfPep[0].Value;
                    player3 = arrayOfPep[1].Value;
                    player4 = arrayOfPep[2].Value;

                    var game = GameState.Instance.FindGameByGroupName(player.Group);

                    if (game != null)
                    {
                        Clients.Group(player.Group).startGame(game);
                        return true;
                    }

                    game = GameState.Instance.CreateGame(player, player2, player3, player4, player.Group);
                    game.WhosTurn = player.ConnectionId;
                    game.GameCount = 1;
                    game.CurrentWind = WindDirection.East;
                    game.Board.Tiles.Shuffle();
                    DistributeTiles(game.Board.Tiles, player, player2, player3, player4);
                    //DistributeTilesForChow(game.Board.Tiles, player, player2, player3, player4);
                    //DistributeTilesForPong(game.Board.Tiles, player, player2, player3, player4);
                    //DistributeTilesForKong(game.Board.Tiles, player, player2, player3, player4);

                    SetPlayerWinds(game);

                    Clients.Group(player.Group).startGame(game);
                    return true;
                }
                else
                {
                    var remainingPep = 3 - pepInGroup.Count();
                    Clients.Caller.waitingList(string.Format("need {0} more player(s)", remainingPep));
                    return false;
                }
            }
            return false;
        }

        public void PlayerMove(string group, string command, int tileId = 0)
        {
            var game = GameState.Instance.FindGameByGroupName(group);
            bool isValidCommand = false;
            string invalidMessage = "shit went wrong...";
            switch (command)
            {
                case "flower":
                    isValidCommand = CommandFlower(game);
                    invalidMessage = "Dude u got no flower";
                    break;
                
                case "go":
                    isValidCommand = CommandGo(game);
                    break;

                case "pick":
                    isValidCommand = CommandPick(game);
                    invalidMessage = "Not ur turn dammit";
                    break;

                case "throw":
                    isValidCommand = CommandThrow(game, tileId);
                    invalidMessage = "stop throwing!";
                    break;

                case "chow":
                    isValidCommand = CommandChow(game);
                    invalidMessage = "nothing to chow --'";
                    break;

                case "pong":
                    isValidCommand = CommandPong(game);
                    invalidMessage = "nothing to pong wth";
                    break;

                case "kong":
                    isValidCommand = CommandKong(game);
                    invalidMessage = "nothing to kong dude";
                    break;

                case "win":
                    isValidCommand = CommandWin(game);
                    invalidMessage = "I let you go this time";
                    break;
            }
            if (isValidCommand)
            {
                SetNextPLayerTurn(game);
                Clients.Group(group).updateGame(game);
            }
            else
            {
                Clients.Group(group).alertUser(invalidMessage);                
            }
            
        }

        private bool CommandPick(Game game)
        {
            var userName = Clients.Caller.name;
            var player = GameState.Instance.GetPlayer(userName);

            if (player != null)
            {
                var newTileForPlayer = game.Board.Tiles.Where(t => t.Owner == "board").First();
                newTileForPlayer.Owner = player.ConnectionId;
            }
            return true;
        }

        private bool CommandThrow(Game game, int tileId)
        {
            var userName = Clients.Caller.name;
            var player = GameState.Instance.GetPlayer(userName);

            if (player != null && tileId > 0)
            {
                var tileToThrow = game.Board.Tiles.Where(t => t.Id == tileId).First();
                tileToThrow.Owner = "graveyard";
                game.LastTile = tileToThrow;

                var newTileForPlayer = game.Board.Tiles.Where(t => t.Owner == "board").First();
                newTileForPlayer.Owner = player.ConnectionId;

                return true;
            }
            else
            {
                return false;
            }
        }

        private bool CommandChow(Game game)
        {
            var userName = Clients.Caller.name;
            var player = GameState.Instance.GetPlayer(userName);

            if (player != null)
            {
                if (CanChow(game, player.ConnectionId))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private bool CanChow(Game game, string userConnectionId)
        {
            var tiles = game.Board.Tiles.Where(t => t.Owner == userConnectionId);
            var thrownTile = game.LastTile;

            if (thrownTile.Type == TileType.Money || thrownTile.Type == TileType.Round || thrownTile.Type == TileType.Stick)
            {
                var matchedTileType = tiles.Where(t => t.Type == thrownTile.Type);
                if (matchedTileType.Count() >= 2)
                {
                    var listTiles = matchedTileType.ToList();
                    int possiblePairs = 0;
                    Tile pairTile1 = new Tile();
                    Tile pairTile2 = new Tile();
                    
                    if (listTiles.Any(t => t.Value == (thrownTile.Value - 2)) && listTiles.Any(t => t.Value == (thrownTile.Value - 1)))                                               
                    {
                        pairTile1 = listTiles.Where(t => t.Value == (thrownTile.Value - 2)).First();
                        pairTile2 = listTiles.Where(t => t.Value == (thrownTile.Value - 1)).First();
                        possiblePairs++;
                    }
                    
                    if(listTiles.Any(t => t.Value == (thrownTile.Value - 1)) && listTiles.Any(t => t.Value == (thrownTile.Value + 1)))
                    {
                        pairTile1 = listTiles.Where(t => t.Value == (thrownTile.Value - 1)).First();
                        pairTile2 = listTiles.Where(t => t.Value == (thrownTile.Value + 1)).First();
                        possiblePairs++;
                    }
                    
                    if (listTiles.Any(t => t.Value == (thrownTile.Value + 1)) && listTiles.Any(t => t.Value == (thrownTile.Value + 2)))
                    {
                        pairTile1 = listTiles.Where(t => t.Value == (thrownTile.Value + 1)).First();
                        pairTile2 = listTiles.Where(t => t.Value == (thrownTile.Value + 2)).First();
                        possiblePairs++;
                    }

                    if(possiblePairs == 1)
                    {
                        var playerTilesChow = new List<Tile>();
                        playerTilesChow.Add(pairTile1);
                        playerTilesChow.Add(pairTile2);
                        playerTilesChow.Add(game.LastTile);
                        CommandTileToPlayerGraveyard(game, playerTilesChow, userConnectionId);
                        game.WhosTurn = userConnectionId;                 
                        return true;    
                    }
                    else if (possiblePairs > 1)
                    {
                        //ask user to pick 2 tiles to chow
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private bool CommandPong(Game game)
        {
            var userName = Clients.Caller.name;
            var player = GameState.Instance.GetPlayer(userName);

            if (player != null)
            {
                if (CanPong(game, player.ConnectionId))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }           
        }

        private bool CanPong(Game game, string userConnectionId)
        {
            var tiles = game.Board.Tiles.Where(t => t.Owner == userConnectionId);
            var thrownTile = game.LastTile;
            var matchedTileTypeAndValue = tiles.Where(t => t.Type == thrownTile.Type && t.Value == thrownTile.Value);
            if (matchedTileTypeAndValue.Count() >= 2)
            {
                var playerTilesPong = matchedTileTypeAndValue.Take(2).ToList();
                playerTilesPong.Add(thrownTile);
                CommandTileToPlayerGraveyard(game, playerTilesPong, userConnectionId);
                game.WhosTurn = userConnectionId;
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool CommandKong(Game game)
        {
            var userName = Clients.Caller.name;
            var player = GameState.Instance.GetPlayer(userName);

            if (player != null)
            {
                if (CanKong(game, player.ConnectionId))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }           
        }

        private bool CanKong(Game game, string userConnectionId)
        {
            var tiles = game.Board.Tiles.Where(t => t.Owner == userConnectionId);
            var thrownTile = game.LastTile;
            var matchedTileTypeAndValue = tiles.Where(t => t.Type == thrownTile.Type && t.Value == thrownTile.Value);
            if (matchedTileTypeAndValue.Count() == 3)
            {
                var playerTilesKong = matchedTileTypeAndValue.Take(3).ToList();
                playerTilesKong.Add(thrownTile);
                CommandTileToPlayerGraveyard(game, playerTilesKong, userConnectionId);
                game.WhosTurn = userConnectionId;
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool CommandWin(Game game)
        {
            var userName = Clients.Caller.name;
            var player = GameState.Instance.GetPlayer(userName);

            if (player != null)
            {
                var newTileForPlayer = game.Board.Tiles.Where(t => t.Owner == "board").First();
                newTileForPlayer.Owner = player.ConnectionId;
            }
            return false;

        }

        private bool CommandFlower(Game game)
        {
            var userName = Clients.Caller.name;
            var player = GameState.Instance.GetPlayer(userName);

            if (player != null)
            {
                var playerTilesFlower = game.Board.Tiles
                    .Where(
                        t => t.Owner == player.ConnectionId &&
                            (t.Value == TileValue.FlowerNumeric || t.Value == TileValue.FlowerRoman));

                if (playerTilesFlower.Count() > 0)
                {
                    CommandTileToPlayerGraveyard(game, playerTilesFlower, player.ConnectionId);
                    var newTileForPlayer = game.Board.Tiles.Where(t => t.Owner == "board").First();
                    newTileForPlayer.Owner = player.ConnectionId;

                    return true;
                }
                else
                {
                    return false;
                }                
            }
            else
            {
                return false;
            }
        }

        private void CommandTileToPlayerGraveyard(Game game, IEnumerable<Tile> tiles, string playerConnectionId)
        { 
            foreach(var f in tiles)
            {
                var tileToGraveyard = game.Board.Tiles.Where(t => t.Id == f.Id).First();
                tileToGraveyard.Owner = "graveyard-" + playerConnectionId;
            }
        }

        private bool CommandGo(Game game)
        {
            //this is just to let go of the turn;
            return true;
            //var userName = Clients.Caller.name;
            //var player = GameState.Instance.GetPlayer(userName);

            //if (player != null)
            //{

            //}
        }

        private void SetPlayerWinds(Game game)
        {
            var gameNo = game.GameCount;
            if (gameNo == 1 || gameNo == 5 || gameNo == 9 || gameNo == 13)
            {
                game.Player1.Wind = WindDirection.East;
                game.Player2.Wind = WindDirection.South;
                game.Player3.Wind = WindDirection.West;
                game.Player4.Wind = WindDirection.North;
            }
            else if (gameNo == 2 || gameNo == 6 || gameNo == 10 || gameNo == 14)
            {
                game.Player1.Wind = WindDirection.North;
                game.Player2.Wind = WindDirection.East;
                game.Player3.Wind = WindDirection.South;
                game.Player4.Wind = WindDirection.West;
            }
            else if (gameNo == 3 || gameNo == 7 || gameNo == 11 || gameNo == 15)
            {
                game.Player1.Wind = WindDirection.West;
                game.Player2.Wind = WindDirection.North;
                game.Player3.Wind = WindDirection.East;
                game.Player4.Wind = WindDirection.South;
            }
            else if (gameNo == 4 || gameNo == 8 || gameNo == 12 || gameNo == 16)
            {
                game.Player1.Wind = WindDirection.South;
                game.Player2.Wind = WindDirection.West;
                game.Player3.Wind = WindDirection.North;
                game.Player4.Wind = WindDirection.East;
            }
        }

        private void SetNextPLayerTurn(Game game)
        {
            if (game != null)
            {
                if (game.WhosTurn == game.Player1.ConnectionId)
                {
                    game.WhosTurn = game.Player2.ConnectionId;
                }             
                else if (game.WhosTurn == game.Player2.ConnectionId)
                {
                    game.WhosTurn = game.Player3.ConnectionId;
                }                
                else if (game.WhosTurn == game.Player3.ConnectionId)
                {
                    game.WhosTurn = game.Player4.ConnectionId;
                }
                else if (game.WhosTurn == game.Player4.ConnectionId)
                {
                    game.WhosTurn = game.Player1.ConnectionId;
                }
            }
        }

        //all of this belong to test section
        //TODO : move this to test
        private void DistributeTilesForKong(List<Tile> tiles, Player p1, Player p2, Player p3, Player p4)
        {
            for (var i = 0; i < 14; i++)
            {
                if (i == 3 || i == 5)
                {
                    continue;
                }
                tiles[i].Owner = p1.ConnectionId;
            }
            tiles[38].Owner = p1.ConnectionId;
            tiles[74].Owner = p1.ConnectionId;
            tiles[110].Owner = p1.ConnectionId;

            for (var i = 14; i < 27; i++)
            {
                tiles[i].Owner = p2.ConnectionId;
            }
            for (var i = 27; i < 40; i++)
            {
                tiles[i].Owner = p3.ConnectionId;
            }
            for (var i = 53; i < 66; i++)
            {
                tiles[i].Owner = p4.ConnectionId;
            }
        }

        private void DistributeTilesForPong(List<Tile> tiles, Player p1, Player p2, Player p3, Player p4)
        {
            for (var i = 0; i < 14; i++)
            {
                if (i == 3 || i == 5)
                {
                    continue;
                }
                tiles[i].Owner = p1.ConnectionId;
            }
            tiles[38].Owner = p1.ConnectionId;
            tiles[74].Owner = p1.ConnectionId;

            for (var i = 14; i < 27; i++)
            {
                tiles[i].Owner = p2.ConnectionId;
            }
            for (var i = 27; i < 40; i++)
            {
                tiles[i].Owner = p3.ConnectionId;
            }
            for (var i = 53; i < 66; i++)
            {
                tiles[i].Owner = p4.ConnectionId;
            }
        }

        private void DistributeTilesForChow(List<Tile> tiles, Player p1, Player p2, Player p3, Player p4)
        {

            for (var i = 0; i < 17; i++)
            {
                if (i == 3 || i == 5 || i == 6)
                {
                    continue;
                }
                tiles[i].Owner = p1.ConnectionId;
            }

            for (var i = 14; i < 27; i++)
            {
                tiles[i].Owner = p2.ConnectionId;
            }
            for (var i = 27; i < 40; i++)
            {
                tiles[i].Owner = p3.ConnectionId;
            }
            for (var i = 40; i < 53; i++)
            {
                tiles[i].Owner = p4.ConnectionId;
            }
        }

        private void DistributeTiles(List<Tile> tiles, Player p1, Player p2, Player p3, Player p4)       
        {
            for (var i = 0; i < 14; i++)
            {                
                tiles[i].Owner = p1.ConnectionId;
            }
            for (var i = 14; i < 27; i++)
            {
                tiles[i].Owner = p2.ConnectionId;
            }
            for (var i = 27; i < 40; i++)
            {
                tiles[i].Owner = p3.ConnectionId;
            }
            for (var i = 40; i < 53; i++)
            {
                tiles[i].Owner = p4.ConnectionId;
            }        
        }

    }
}