using System;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();
        foreach (var move in moves)
        {
            if (MoveIsCheckmate(board, move)) return move;
        }
        foreach (var move in moves)
        {
            if (IsCheckMateOnNextMove(board, move)) return move;
        }

        foreach (var move in moves)
        {
            if (move.IsPromotion && !MovedPeaceMayBeEaten(move, board) &&
                move.PromotionPieceType == PieceType.Queen) return move;
        }
        
        Move captureMove = default;
        void SaveMoveIfBest(Move move, bool preferable = false)
        {
            if (move.CapturePieceType < captureMove.CapturePieceType) return;
            if (!preferable && move.CapturePieceType == captureMove.CapturePieceType) return;
            captureMove = move;
        }
        foreach (var move in moves)
        {
            if (move.IsCapture)
            {
                if (move.MovePieceType < move.CapturePieceType) SaveMoveIfBest(move);
                if (!MovedPeaceMayBeEaten(move, board)) SaveMoveIfBest(move, true);
            }
        }

        if (captureMove.IsCapture) return captureMove;

        var rnd = new Random();
        return moves[rnd.Next(moves.Length)];
    }

    static bool MovedPeaceMayBeEaten(Move move, Board board)
    {
        return MakeMoveAndDoFunc(boardAfterMove =>
        {
            var squareToCheck = move.TargetSquare;
            var opponentCaptures = boardAfterMove.GetLegalMoves(true);
            return opponentCaptures.Any(capture => capture.TargetSquare == squareToCheck);
        }, board, move);
    }
    
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

    static TResult MakeMoveAndDoFunc<TResult>(Func<Board, TResult> funcOnBoardAfterMove, Board board, Move move)
    {
        board.MakeMove(move);
        var result = funcOnBoardAfterMove.Invoke(board);
        board.UndoMove(move);
        return result;
    }
}