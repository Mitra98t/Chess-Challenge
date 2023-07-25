using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ChessChallenge.API;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class MyBot : IChessBot
{
    int[] pieceValues = { 0, 100, 300, 300, 500, 900, 100000 };
    List<Move> LastMoves = new();
    double endgameWeight = 0;
    Board board;
    int piecesOnBoard = 32;
    public Move Think(Board board, Timer timer)
    {
        this.board = board;

        // return StartSearch(3);
        //Low on time
        piecesOnBoard = BitboardHelper.GetNumberOfSetBits(board.AllPiecesBitboard);
        if (piecesOnBoard < 4)
            return StartSearch(5);
        if (piecesOnBoard < 8)
            return StartSearch(4);
        else
            return StartSearch(3);
    }

    public Move[] GetOrderMoves(bool onlyCaptures = false)
    {
        List<(int, Move)> moveScores = new();
        Move[] moves = board.GetLegalMoves(onlyCaptures);
        foreach (Move move in moves)
        {
            int moveScoreGuess = 0;
            PieceType movePieceType = move.MovePieceType; // Piece i'm moving
            PieceType capturePieceType = board.GetPiece(move.TargetSquare).PieceType; // Piece i'm capturing

            //Encourage Piece Development
            if (movePieceType == PieceType.Knight || movePieceType == PieceType.Bishop || movePieceType == PieceType.Rook)
                moveScoreGuess += 80;

            // Encourage Castling
            if (move.IsCastles)
                moveScoreGuess += 100;

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

            moveScores.Add((moveScoreGuess, move));
        }

        moveScores.Sort((x, y) => y.Item1 - x.Item1);
        List<Move> movesSorted = new();

        moveScores.ForEach(x => movesSorted.Add(x.Item2));
        return movesSorted.ToArray();
    }

    private int EvalPosition()
    {
        int value = 0;

        PieceList[] piecelists = board.GetAllPieceLists();

        foreach (PieceList piece_list in piecelists)
        {
            int multiplier = piece_list.IsWhitePieceList ? 1 : -1;

            foreach (Piece piece in piece_list)
            {
                value += multiplier * (pieceValues[(int)piece.PieceType] + MobilityEvaluation(piece));
                // value += multiplier * pieceValues[(int)piece.PieceType];
            }
        }
        value += ForceKingToCornerEval() + KingSafetyEvaluation();
        if(board.IsInCheckmate())
            value += 1000000;
        return value * (board.IsWhiteToMove ? 1 : -1);
    }

    int SearchAllCaptures(int alpha, int beta)
    {
        int evaluation = EvalPosition();
        if (evaluation >= beta)
            return beta;

        alpha = Math.Max(alpha, evaluation);

        //order move
        Move[] moves = GetOrderMoves(true);

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            evaluation = -SearchAllCaptures(-beta, -alpha);
            board.UndoMove(move);
            if (evaluation >= beta)
                return beta;
            alpha = Math.Max(alpha, evaluation);
        }

        return alpha;
    }

    // TODO make single function incorporating searchAllCaptures
    int Search(int depth, int alpha, int beta)
    {
        if (depth == 0)
        {
            // Stop searching at the leaf nodes and evaluate the position
            // return EvalPosition();

            // Search all captures at the leaf nodes
            return SearchAllCaptures(alpha, beta);
        }

        Move[] moves = GetOrderMoves();
        if (moves.Length == 0)
        {
            if (board.IsInCheck())
                return -100000;
            else
                return 0;
        }

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int evaluation = -Search(depth - 1, -beta, -alpha);
            board.UndoMove(move);
            if (evaluation >= beta)
                return beta;
            alpha = Math.Max(alpha, evaluation);
        }
        return alpha;
    }


    private Move StartSearch(int depth)
    {
        Move[] moves = GetOrderMoves();
        Move best_move = moves[0];
        int bestScore = -100000;

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int move_score = -Search(depth, -100000, 100000);
            board.UndoMove(move);
            if (move_score > bestScore && !WillBeRepetition(move))
            // if (move_score > bestScore)
            {
                bestScore = move_score;
                best_move = move;
            }
        }
        AddToListMoves(best_move);
        return best_move;
    }

    private int MobilityEvaluation(Piece piece)
    {
        int value;
        int slidingPieceControl = BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetSliderAttacks(piece.PieceType, piece.Square, board));
        switch (piece.PieceType)
        {
            case PieceType.Knight:
                value = BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetKnightAttacks(piece.Square)) * 10;
                break;
            case PieceType.Bishop:
                value = slidingPieceControl * 10;
                break;
            case PieceType.Queen:
                value = slidingPieceControl * 15;
                break;
            case PieceType.Rook:
                value = slidingPieceControl * 17;
                break;
            default:
                value = 0;
                break;
        }
        return value;
    }


    private int ForceKingToCornerEval()
    {
        endgameWeight = (32-piecesOnBoard) / 32.0;
        Square friendlyKingSquare = board.GetKingSquare(board.IsWhiteToMove);
        Square opponentKingSquare = board.GetKingSquare(!board.IsWhiteToMove);

        // Opponent king away from center
        // Calculate the distance of the king from the center
        // int a = Math.Max(3 - opponentKingSquare.File, opponentKingSquare.File - 4);
        // int b = Math.Max(3 - opponentKingSquare.Rank, opponentKingSquare.Rank - 4);
        // int dist1 = a + b;
        // evaluation += dist1;
        int evaluation = Math.Max(3 - opponentKingSquare.File, opponentKingSquare.File - 4) + Math.Max(3 - opponentKingSquare.Rank, opponentKingSquare.Rank - 4);

        // Friendly king near opponent king
        // sum of the distance between the two kings
        // int c = Math.Abs(friendlyKingSquare.File - opponentKingSquare.File);
        // int d = Math.Abs(friendlyKingSquare.Rank - opponentKingSquare.Rank);
        // int dist2 = c + d;
        // evaluation += 14 - dist2;
        evaluation += 14 - (Math.Abs(friendlyKingSquare.File - opponentKingSquare.File) + Math.Abs(friendlyKingSquare.Rank - opponentKingSquare.Rank));

        return (int)(evaluation * 20 * endgameWeight);
    }

    private void AddToListMoves(Move move)
    {
        LastMoves.Add(move);
        if (LastMoves.Count > 4)
            LastMoves.RemoveAt(0);

    }

    private bool WillBeRepetition(Move move)
    {
        return LastMoves.Count >= 2 && LastMoves[^2].StartSquare == move.StartSquare && LastMoves[^2].TargetSquare == move.TargetSquare && LastMoves[^3].StartSquare == LastMoves[^1].StartSquare && LastMoves[^3].TargetSquare == LastMoves[^1].TargetSquare;
    }

    private int KingSafetyEvaluation()
    {
        int evaluation = 0;
        int targetRank = board.IsWhiteToMove ? 0 : 7;
        Square kingSquare = board.GetKingSquare(board.IsWhiteToMove);
        if (kingSquare.Rank == targetRank)
            evaluation += 50 + Math.Abs(4 - kingSquare.File) * 20;

        return (int)(evaluation * (piecesOnBoard / 50));
    }

}