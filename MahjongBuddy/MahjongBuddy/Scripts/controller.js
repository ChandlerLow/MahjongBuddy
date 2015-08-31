﻿app.controller("MahjongController", ['$scope', '$timeout', 'signalRHubProxy', 'mjService',
    function ($scope, $timeout, signalRHubProxy, mjService) {
        $scope.isDisonnected = true;
        $scope.gameIsReady = false;
        $scope.selectedTiles = [];
        $scope.isMyturn = false;
        $scope.record = {};
        $scope.canPickTile = false;
        $scope.hideGameCountdown = true;
        $scope.gameEndTimeout = null;
        $scope.tenSectimeout = null;
        $scope.hideAlert = true;

        var FindTileByIndexAndPlayerConnection = function (game, tileIndex, pid) {
            var ret = null;
            $.each(game.Board.Tiles, function (idx, val) {
                if (val.Owner == pid && val.ActiveTileIndex == tileIndex) {
                    ret = val;
                }
            });
            return ret;
        }
        var startup = function(conId){
            $scope.isDisonnected = !(conId != undefined);
            $scope.currentUserId = conId;
        };
        
        var cancelGameEndCountDown = function () {
            // we stop the countdown to show the shownowinner modal
            if ($scope.gameEndTimeout != null && $scope.gameEndTimeout != undefined) {
                $timeout.cancel($scope.gameEndTimeout);
            }
            $scope.hideGameCountdown = true;
            $scope.gameEndCountdown = 10;
        }

        var clientPushHubProxy = signalRHubProxy(signalRHubProxy.defaultServer, 'gameHub', startup);

        var updateGame = function (currentPlayer) {

            $scope.currentPlayer = currentPlayer
            $scope.rightPlayer = currentPlayer.RightPlayer;
            $scope.topPlayer = currentPlayer.TopPlayer;
            $scope.leftPlayer = currentPlayer.LeftPlayer;
            $scope.currentPlayerWind = currentPlayer.Wind;

            $scope.isMyturn = currentPlayer.CanPickTile;

            if ($scope.isMyturn) {
                $timeout(function () { $scope.canPickTile = $scope.currentPlayer.CanPickTile }, 5000);
            }

            //$scope.lastTile = game.LastTile;
            $scope.currentGameWind = mjService.getWindName($scope.game.CurrentWind);
        }

        var updateOtherPlayer = function (game) {
            mjService.setPlayer(game, $scope.currentUserId);
            $scope.topPlayer = mjService.topPlayer;
            $scope.rightPlayer = mjService.rightPlayer;
            $scope.leftPlayer = mjService.leftPlayer;
        }

        $scope.join = function () {
            var un = $('#usernameTB').val();
            clientPushHubProxy.invoke2('Join', un, 'mjbuddy', function (){});
        };

        $scope.resetGame = function () {
            clientPushHubProxy.invoke('ResetGame', function () {});
        };

        $scope.fnIsTileActive = function (tileId) {
            return $.inArray(tileId, $scope.selectedTiles) > -1;
        }

        $scope.fnSelectTile = function (tileId, evt) {

            var isActive = evt.currentTarget.className.indexOf("activeTile") > -1

            if (isActive) {
                //remove tile
                $scope.selectedTiles = $.grep($scope.selectedTiles, function (e) {
                    return e !== tileId;
                });
            } else {
                $scope.selectedTiles.push(tileId)
            }
        };

        $scope.fnStartNextGame = function () {
            clientPushHubProxy.invoke1('StartNextGame', 'mjbuddy', function (){});
        };

        $scope.fnPlayerMove = function (move, tiles) {
            $scope.warningMessage = "";
            clientPushHubProxy.invoke3('PlayerMove', 'mjbuddy', move, tiles, function (game) {});
            //clear the selected tiles for every player move
            $scope.selectedTiles = [];
        };

        $scope.onDropComplete = function (index, obj, evt) {

            var targetTile = FindTileByIndexAndPlayerConnection($scope.game, index, obj.Owner);

            targetTile.ActiveTileIndex = obj.ActiveTileIndex;
            obj.ActiveTileIndex = index;
            $scope.currentPlayer.IsTileAutoSort = false;
            $scope.selectedTiles = [];
        };

        $scope.fnToggleSortTile = function () {

        };


///**************************        
//clientPushHubProxy section
///**************************
        clientPushHubProxy.on('showWinner', function (game) {
            cancelGameEndCountDown();
            $scope.record = game.Records[game.Records.length - 1];
            updateOtherPlayer(game);
            updateGame(game);
            $("#showWinnerModal").modal('show');
        });

        clientPushHubProxy.on('showNoWinner', function (game) {
            $scope.record = game.Records[game.Records.length - 1];
            updateOtherPlayer(game);
            updateGame(game);

            $scope.hideGameCountdown = false;
            $scope.gameEndCountdown = 10;
            $scope.onTimeout = function () {
                $scope.tenSectimeout = $timeout($scope.onTimeout, 1000);
                $scope.gameEndCountdown--;
            }
            $scope.tenSectimeout = $timeout($scope.onTimeout, 1000);
            
            if ($scope.gameEndCountdown <= 0)
            {
                $timeout.cancel($scope.tenSectimeout);
                $scope.onTimeout = null
            }
            $scope.gameEndTimeout = $timeout(function () {
                $("#showNoWinnerModal").modal('show');
                $scope.hideGameCountdown = true;
            }, 50000);
        });

        clientPushHubProxy.on('notifyUserInGroup', function (msg) {
            $('#gameStatus').append(msg + "<br/>");
        });
        
        clientPushHubProxy.on('playerJoined', function (user) {
            $('#usernameTB').attr('disabled', 'disabled');
            $('#join').attr('disabled', 'disabled');
        });

        clientPushHubProxy.on('waitingList', function (msg) {
            $('#gameStatus').append(msg + "<br/>");
        });

        clientPushHubProxy.on('gameStarted', function () {
            $scope.gameIsReady = true;
            $('#gameStatus').append("Game Started!  <br/>");
            $('#GameRoom').show();
            $('#WaitingRoom').hide();
        });

        clientPushHubProxy.on('startGame', function (game) {
            mjService.setPlayer(game, $scope.currentUserId);
            $scope.topPlayer = mjService.topPlayer;
            $scope.rightPlayer = mjService.rightPlayer;
            $scope.leftPlayer = mjService.leftPlayer;
            updateGame(game);
        });

        clientPushHubProxy.on('startNextGame', function (game) {
            updateGame(game);
            $('#showWinnerModal').modal('hide');
            $('#showNoWinnerModal').modal('hide');
        });

        clientPushHubProxy.on('updateGame', function (game) {
            updateGame(game);
        });

        clientPushHubProxy.on('updatePlayerCount', function (playerCount) {
            $scope.totalPlayers = playerCount;
        });

        clientPushHubProxy.on('alertUser', function (msg) {
            $scope.warningMessage = msg;
            $scope.hideAlert = false;
            $timeout(function () {
                $scope.hideAlert = true;
            }, 4000);
        });
    }
]);





