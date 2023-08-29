using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    private static readonly Random rnd = new();

    // TODO 3: Make depth as parameter of all strategies. Set up depth on the base of time left.
    
    public Move Think(Board board, Timer timer)
    {
        LastMoveStrategy = 0;

        Move[] allMoves = board.GetLegalMoves();
        if (allMoves.Length == 0) return new Move(); // Can happen if called from this class

        var moves = AfraidOfLosing ? FilterMoves(allMoves, board, 1) : allMoves;
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

        // TODO 2: Filter moves after which we are captured, or at least cheapest piece will be captured.
        var newMoves = AfraidOfLosing ? FilterMoves(moves, board, 3) : moves;

        return newMoves.Length > 0
            ? newMoves[rnd.Next(newMoves.Length)]
            : moves[rnd.Next(moves.Length)];
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
    bool FindCaptureMove(Board board, IReadOnlyList<Move> moves, out Move captureMove)
    {
        captureMove = TryGetCaptureMove(board, moves, AfraidOfLosing);
        return captureMove.IsCapture;
    }

    static Move TryGetCaptureMove(Board board, IReadOnlyList<Move> moves, bool lookForTwoMovesCapture = true, bool allowSacrifice = true)
    {
        Move resultMove = default;
        void SaveMoveIfBest(Move move, PieceType capturedPiece, bool preferable = false)
        {
            if (capturedPiece < resultMove.CapturePieceType) return;
            if (!preferable && capturedPiece == resultMove.CapturePieceType) return;
            resultMove = move;
        }
        foreach (var move in moves)
        {
            if (move.IsCapture)
            {
                if (allowSacrifice && move.MovePieceType < move.CapturePieceType) SaveMoveIfBest(move, move.CapturePieceType);
                if (!MovedPeaceMayBeEaten(move, board)) SaveMoveIfBest(move, move.CapturePieceType, true);
            }
            else if (lookForTwoMovesCapture)
            {
                // Try to capture in two moves
                var capturedInTwoMoves = MakeMoveAndDoFunc(FindBestCaptureAfterOpponentMove, board, move);
                if (capturedInTwoMoves > 0) SaveMoveIfBest(move, capturedInTwoMoves);
            }
        }
        // TODO 4: Think of case when moved piece was a protector.
        // After the capture move another our piece (not moved one) may be captured by opponent.
        // So we may lose more valuable piece than the captured one.
        return resultMove;
    }

    static PieceType FindBestCaptureAfterOpponentMove(Board board)
    {
        var guaranteedCapturedPiece = PieceType.King;
        var opponentMoves = board.GetLegalMoves();
        if (!opponentMoves.Any()) return PieceType.None;
        foreach (var opponentMove in opponentMoves)
        {
            PieceType lostPiece = PieceType.None;
            if (opponentMove.IsCapture) lostPiece = opponentMove.CapturePieceType;
            var bestCapture =
                MakeMoveAndDoFunc(
                    boardAfterMove => TryGetCaptureMove(
                        boardAfterMove, 
                        boardAfterMove.GetLegalMoves(true), 
                        false,
                    lostPiece == PieceType.None), // We can sacrifice one piece only on the first or on the second opponent move.
                    board, opponentMove);
            if (bestCapture.CapturePieceType <= lostPiece) return PieceType.None; // After at least one opponent move we cannot do a meaningful capture.
            if (bestCapture.CapturePieceType < guaranteedCapturedPiece) guaranteedCapturedPiece = bestCapture.CapturePieceType;
        }
        return guaranteedCapturedPiece;
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

    static Move[] FilterMoves(IReadOnlyList<Move> moves, Board board, int enemyStrategyFilter)
    {
        var otherBot = new MyBot() { AfraidOfLosing = false };
        Timer timer = new(100000);
        return moves.Where(move => MakeMoveAndDoFunc(boardAfterMove =>
        {
            otherBot.Think(boardAfterMove, timer);
            return otherBot.LastMoveStrategy != enemyStrategyFilter;
        }, board, move)).ToArray();
    }

    #endregion

    #region Checkmate helpers
    static bool MoveIsCheckmate(Board board, Move move)
    {
        return MakeMoveAndDoFunc(boardAfterMove => boardAfterMove.IsInCheckmate(), board, move);
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