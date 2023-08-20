﻿using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        LastMoveStrategy = 0;

        Move[] allMoves = board.GetLegalMoves();
        if (allMoves.Length == 0) return new Move(); // Can happen if called from this class

        var moves = AfraidOfLosing ? FilterMoves(allMoves, board) : allMoves;
        if (moves.Length == 0) {
            Console.WriteLine("Failure predicted.");
            return allMoves[0]; // We are definitely lost
        }

        Move bestMove;
        if (FindCheckMateMove(board, moves, out bestMove))
        {
            LastMoveStrategy = 1;
            return bestMove;
        }
        if (FindPromotionMove(board, moves, out bestMove))
        {
            LastMoveStrategy = 2;
            return bestMove;
        }
        if (FindCaptureMove(board, moves, out bestMove))
        {
            LastMoveStrategy = 3;
            return bestMove;
        }

        var rnd = new Random();
        return moves[rnd.Next(moves.Length)];
    }

    #region Properties
    private int LastMoveStrategy { get; set; } = 0;

    private bool AfraidOfLosing { get; set; } = true;
    #endregion

    #region Strategies

    #region Checkmate
    static bool FindCheckMateMove(Board board, IReadOnlyList<Move> moves, out Move checkmateMove)
    {
        checkmateMove = default;
        foreach (var move in moves)
        {
            if (MoveIsCheckmate(board, move))
            {
                checkmateMove = move;
                return true;
            }
        }
        foreach (var move in moves)
        {
            if (IsCheckMateOnNextMove(board, move))
            {
                checkmateMove = move;
                return true;
            }
        }
        return false;
    }
    

    #endregion

    #region Promotion

    static bool FindPromotionMove(Board board, IReadOnlyList<Move> moves, out Move promotionMove)
    {
        promotionMove = default;
        foreach (var move in moves)
        {
            if (move.IsPromotion && !MovedPeaceMayBeEaten(move, board) &&
                move.PromotionPieceType == PieceType.Queen)
            {
                promotionMove = move;
                return true;
            }
        }
        return false;
    }
    

    #endregion

    #region Capture
    static bool FindCaptureMove(Board board, IReadOnlyList<Move> moves, out Move captureMove)
    {
        Move resultMove = default;
        void SaveMoveIfBest(Move move, bool preferable = false)
        {
            if (move.CapturePieceType < resultMove.CapturePieceType) return;
            if (!preferable && move.CapturePieceType == resultMove.CapturePieceType) return;
            resultMove = move;
        }
        foreach (var move in moves)
        {
            if (move.IsCapture)
            {
                if (move.MovePieceType < move.CapturePieceType) SaveMoveIfBest(move);
                if (!MovedPeaceMayBeEaten(move, board)) SaveMoveIfBest(move, true);
            }
        }
        captureMove = resultMove;
        return resultMove.IsCapture;
    }
    #endregion

    #endregion

    #region Helpers

    #region Move validation
    static bool MovedPeaceMayBeEaten(Move move, Board board)
    {
        return MakeMoveAndDoFunc(boardAfterMove =>
        {
            var squareToCheck = move.TargetSquare;
            var opponentCaptures = boardAfterMove.GetLegalMoves(true);
            return opponentCaptures.Any(capture => capture.TargetSquare == squareToCheck);
        }, board, move);
    }

    static Move[] FilterMoves(IReadOnlyList<Move> moves, Board board)
    {
        var otherBot = new MyBot();
        otherBot.AfraidOfLosing = false;
        var timer = new Timer(100000);
        return moves.Where(move => MakeMoveAndDoFunc(boardAfterMove =>
        {
            otherBot.Think(boardAfterMove, timer);
            return otherBot.LastMoveStrategy != 1;
        }, board, move)).ToArray();
    }
    #endregion

    #region Checkmate helpers
// Test if this move gives checkmate
    static bool MoveIsCheckmate(Board board, Move move)
    {
        board.MakeMove(move);
        bool isMate = board.IsInCheckmate();
        board.UndoMove(move);
        return isMate;
    }

    static bool IsCheckMateOnNextMove(Board board, Move currentMove)
    {
        return MakeMoveAndDoFunc(IsCheckMateOnNextMove, board, currentMove);
    }
    
    static bool IsCheckMateOnNextMove(Board board)
    {
        var opponentMoves = board.GetLegalMoves();
        foreach (var opponentMove in opponentMoves)
        {
            board.MakeMove(opponentMove);
            var ourMoves = board.GetLegalMoves();
            bool success = false;
            foreach (var ourMove in ourMoves)
            {
                if (MoveIsCheckmate(board, ourMove))
                {
                    Console.WriteLine($"Opponent move: {opponentMove}, our move: {ourMove}.");
                    success = true;
                    break;
                }
            }
            board.UndoMove(opponentMove);
            if (!success) return false;
        }
        
        Console.WriteLine($"It has to be checkmate for {(board.IsWhiteToMove ? "Black" : "White")} ");
        Console.WriteLine(board.CreateDiagram());
        return true;
    }
    #endregion

    #region Syntax sugar
    static TResult MakeMoveAndDoFunc<TResult>(Func<Board, TResult> funcOnBoardAfterMove, Board board, Move move)
    {
        board.MakeMove(move);
        var result = funcOnBoardAfterMove.Invoke(board);
        board.UndoMove(move);
        return result;
    }
    #endregion

    #endregion
}