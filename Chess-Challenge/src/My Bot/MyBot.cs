using System;
using System.Collections.Generic;
using ChessChallenge.API;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class MyBot : IChessBot
{
    int[] pieceValues = { 0, 100, 300, 300, 500, 900, 100000 };


    Board board;
    public Move Think(Board board, Timer timer)
    {
        this.board = board;

        //Low on time
        if (timer.MillisecondsRemaining < 10000)
        {
            return startSearch(2);
        }
        else if (timer.MillisecondsRemaining < 30000)
        {
            return startSearch(3);
        }
        else
        {
            return startSearch(4);
        }
    }

    public Move[] orderMoves(Move[] moves)
    {
        List<int> moveScores = new List<int>();
        foreach (Move move in moves)
        {
            int moveScoreGuess = 0;
            PieceType movePieceType = move.MovePieceType; // Piece i'm moving
            PieceType capturePieceType = board.GetPiece(move.TargetSquare).PieceType; // Piece i'm capturing

            if (capturePieceType != PieceType.None)
                moveScoreGuess = 10 * pieceValues[(int)capturePieceType] - pieceValues[(int)movePieceType];

            //TODO check this evaluation
            if (move.IsPromotion)
                moveScoreGuess += pieceValues[(int)move.PromotionPieceType];

            // Check all piece 
            //TODO should be only attacked by pawns
            if (board.SquareIsAttackedByOpponent(move.TargetSquare))
                moveScoreGuess -= pieceValues[(int)movePieceType];

            if (board.SquareIsAttackedByOpponent(move.StartSquare))
                moveScoreGuess += pieceValues[(int)movePieceType];

            if (movePieceType == PieceType.King)
                moveScoreGuess -= 10 * pieceValues[(int)movePieceType];

            moveScores.Add(moveScoreGuess);
        }

        for (int i = 0; i < moves.Length - 2; i++)
        {
            for (int j = i + 1; j < moves.Length - 1; j++)
            {
                if (moveScores[i] < moveScores[j])
                {
                    (moves[j], moves[i]) = (moves[i], moves[j]);
                    (moveScores[j], moveScores[i]) = (moveScores[i], moveScores[j]);
                }
            }
        }

        return moves;
    }

    private int evalPosition()
    {
        int value = 0;

        PieceList[] piecelists = board.GetAllPieceLists();

        foreach (PieceList piece_list in piecelists)
        {
            int multiplier = piece_list.IsWhitePieceList ? 1 : -1;

            foreach (Piece piece in piece_list)
            {
                value += multiplier * pieceValues[(int)piece.PieceType];
            }
        }
        return value * (board.IsWhiteToMove ? 1 : -1);
    }

    int searchAllCaptures(int alpha, int beta)
    {
        int evaluation = evalPosition();
        if (evaluation >= beta)
            return beta;

        alpha = Math.Max(alpha, evaluation);

        //order move
        Move[] moves = orderMoves(board.GetLegalMoves(true));

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            evaluation = -searchAllCaptures(-beta, -alpha);
            board.UndoMove(move);
            if (evaluation >= beta)
            {
                return beta;
            }
            alpha = Math.Max(alpha, evaluation);
        }

        return alpha;
    }

    int search(int depth, int alpha, int beta)
    {
        if (depth == 0)
        {
            // Stop searching at the leaf nodes and evaluate the position
            // return evalPosition();

            // Search all captures at the leaf nodes
            return searchAllCaptures(alpha, beta);
        }

        Move[] moves = orderMoves(board.GetLegalMoves());
        if (moves.Length == 0)
        {
            if (board.IsInCheck())
            {
                return -100000;
            }
            else
            {
                return 0;
            }
        }

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int evaluation = -search(depth - 1, -beta, -alpha);
            board.UndoMove(move);
            if (evaluation >= beta)
            {
                return beta;
            }
            alpha = Math.Max(alpha, evaluation);
        }
        return alpha;
    }


    private Move startSearch(int depth)
    {
        Move[] moves = orderMoves(board.GetLegalMoves());
        //random number
        Move best_move = moves[0];

        int score = -100000;
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int move_score = -search(depth, -100000, 100000);
            board.UndoMove(move);

            if (move_score > score)
            {
                score = move_score;
                best_move = move;
            }
        }

        return best_move;
    }


}