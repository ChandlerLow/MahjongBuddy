﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MahjongBuddy.Models
{
    public enum CommandResult
    {
        ValidCommand,
        ValidPick,
        ValidThrow,
        InvalidPong,
        InvalidKong,
        InvalidChow,
        InvalidPick,
        InvalidThrow,
        InvalidChowTileType,
        InvalidChowNeedTwoTiles,
        InvalidPlayer,
        PlayerWin,
        PlayerWinFailed,
        InvalidWin,
        SomethingWentWrong,
        InvalidPickWentWrong,
        NobodyWin,
        WinNotEnoughPoint
    }
}