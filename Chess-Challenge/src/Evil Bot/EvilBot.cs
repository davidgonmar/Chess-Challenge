using ChessChallenge.API;
using System;
using System.Linq;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBot : IChessBot
    {
        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };

        public Move Think(Board board, Timer timer)
        {
            Move[] allMoves = board.GetLegalMoves();

            // Pick a random move to play if nothing better is found
            Random rng = new();
            Move moveToPlay = allMoves[rng.Next(allMoves.Length)];
            int highestValueCapture = 0;

            foreach (Move move in allMoves)
            {
                // Always play checkmate in one
                if (MoveIsCheckmate(board, move))
                {
                    moveToPlay = move;
                    break;
                }

                // Find highest value capture
                Piece capturedPiece = board.GetPiece(move.TargetSquare);
                int capturedPieceValue = pieceValues[(int)capturedPiece.PieceType];

                if (capturedPieceValue > highestValueCapture)
                {
                    moveToPlay = move;
                    highestValueCapture = capturedPieceValue;
                }
            }

            return moveToPlay;
        }

        // Test if this move gives checkmate
        bool MoveIsCheckmate(Board board, Move move)
        {
            board.MakeMove(move);
            bool isMate = board.IsInCheckmate();
            board.UndoMove(move);
            return isMate;
        }
    }

    // test bot I got from a repo
    public class MyABMinimax : IChessBot
    {
        public const int TIME_PER_MOVE = 2500;
        public const int INITIAL_DEPTH = 4;
        public int turns = 0;
        public Move Think(Board board, Timer timer)
        {
            // Move[] moves = board.GetLegalMoves();
            // return moves[0];

            // Move[] legalMoves = board.GetLegalMoves();
            // Move[] captures = board.GetLegalMoves(capturesOnly: true);
            // //randomly shuffle legal moves
            // Random rnd = new Random();
            // legalMoves = legalMoves.OrderBy(x => rnd.Next()).ToArray();
            Move[] captureMoves = board.GetLegalMoves(capturesOnly: true);
            Move[] nonCaptureMoves = board.GetLegalMoves(capturesOnly: false).Except(captureMoves).ToArray();
            //randomly shuffle capture moves and non capture moves
            Random rnd = new Random();
            captureMoves = captureMoves.OrderBy(x => rnd.Next()).ToArray();
            nonCaptureMoves = nonCaptureMoves.OrderBy(x => rnd.Next()).ToArray();
            //create legalMoves array, with captures first then non captures
            Move[] legalMoves = new Move[captureMoves.Length + nonCaptureMoves.Length];
            Array.Copy(captureMoves, legalMoves, captureMoves.Length);
            Array.Copy(nonCaptureMoves, 0, legalMoves, captureMoves.Length, nonCaptureMoves.Length);

            int num_legal_moves = legalMoves.Length;
            //set a random move as best to start
            Move bestMove = legalMoves[0];
            double bestValue = double.NegativeInfinity;
            int boardPieces = getNumPieces(board);
            int depthLeft = INITIAL_DEPTH;
            if (boardPieces <= 16)
            {
                depthLeft++;
            }
            if (boardPieces <= 12)
            {
                depthLeft++;
            }
            if (boardPieces <= 8)
            {
                depthLeft++;
            }
            if (boardPieces <= 6)
            {
                depthLeft++;
            }
            if (boardPieces <= 4)
            {
                depthLeft++;
            }
            if (timer.MillisecondsRemaining < 30000)
            {
                depthLeft--;
            }
            if (timer.MillisecondsRemaining < 15000)
            {
                depthLeft--;
            }
            if (timer.MillisecondsRemaining < 10000)
            {
                depthLeft--;
            }
            if (timer.MillisecondsRemaining < 5000)
            {
                depthLeft--;
            }
            int movesChecked = 0;
            bool Reduced = false;
            foreach (Move move in legalMoves)
            {
                if (timer.MillisecondsElapsedThisTurn > TIME_PER_MOVE && Reduced == false)
                {
                    depthLeft -= 2;
                    Reduced = true;
                }
                board.MakeMove(move);
                double boardValue = Minimax(board, depthLeft, 0, false, !board.IsWhiteToMove, timer, double.NegativeInfinity, double.PositiveInfinity);

                if (boardValue > bestValue)
                {
                    bestValue = boardValue;
                    bestMove = move;
                }
                board.UndoMove(move);
                movesChecked++;
            }
            turns++;
            return bestMove;
        }
        public int getNumPieces(Board board)
        {
            int numPieces = 0;
            PieceList[] pieces = board.GetAllPieceLists();
            foreach (PieceList pieceList in pieces)
            {
                numPieces += pieceList.Count;
            }
            return numPieces;
        }

        public double Minimax(Board board, int depthLeft, int depthSoFar, bool isMaximizingPlayer, bool rootIsWhite, Timer timer, double alpha, double beta)
        {
            if (depthLeft == 0 || board.IsInCheckmate() || board.IsDraw())
            {
                return EvaluateBoard(board, rootIsWhite, depthSoFar);
            }

            Move[] captureMoves = board.GetLegalMoves(capturesOnly: true);
            Move[] nonCaptureMoves = board.GetLegalMoves(capturesOnly: false).Except(captureMoves).ToArray();
            //randomly shuffle capture moves and non capture moves
            Random rnd = new Random();
            captureMoves = captureMoves.OrderBy(x => rnd.Next()).ToArray();
            nonCaptureMoves = nonCaptureMoves.OrderBy(x => rnd.Next()).ToArray();
            //create legalMoves array, with captures first then non captures
            Move[] legalMoves = new Move[captureMoves.Length + nonCaptureMoves.Length];
            Array.Copy(captureMoves, legalMoves, captureMoves.Length);
            Array.Copy(nonCaptureMoves, 0, legalMoves, captureMoves.Length, nonCaptureMoves.Length);


            if (isMaximizingPlayer)
            {
                double maxEval = double.NegativeInfinity;

                foreach (Move move in legalMoves)
                {
                    board.MakeMove(move);
                    double eval = Minimax(board, depthLeft - 1, depthSoFar + 1, false, rootIsWhite, timer, alpha, beta);
                    board.UndoMove(move);
                    maxEval = Math.Max(maxEval, eval);

                    // Alpha-beta pruning decision
                    alpha = Math.Max(alpha, eval);
                    if (beta <= alpha)
                    {
                        break;
                    }
                }

                return maxEval;
            }
            else
            {
                double minEval = double.PositiveInfinity;

                foreach (Move move in legalMoves)
                {
                    board.MakeMove(move);
                    double eval = Minimax(board, depthLeft - 1, depthSoFar + 1, true, rootIsWhite, timer, alpha, beta);
                    board.UndoMove(move);
                    minEval = Math.Min(minEval, eval);

                    // Alpha-beta pruning decision
                    beta = Math.Min(beta, eval);
                    if (beta <= alpha)
                    {
                        break;
                    }
                }

                return minEval;
            }
        }
        public double EvaluateBoard(Board board, bool rootIsWhite, int depthSoFar)
        {
            double whiteScore = 0;
            double blackScore = 0;
            PieceList[] pieces = board.GetAllPieceLists();
            if (board.IsDraw())
            {
                return -1;
            }
            if (board.IsInCheckmate())
            {
                if (board.IsWhiteToMove)
                {
                    whiteScore -= 9999999999 - depthSoFar;
                }
                else
                {
                    blackScore -= 9999999999 - depthSoFar;
                }
                if (rootIsWhite)
                {
                    return whiteScore - blackScore;
                }
                else
                {
                    return blackScore - whiteScore;
                }
            }
            foreach (PieceList pieceList in pieces)
            {
                if (pieceList.TypeOfPieceInList == PieceType.Pawn)
                { //pawn
                    if (pieceList.IsWhitePieceList)
                    {
                        whiteScore += 100 * pieceList.Count;
                        // get the pawns square
                        for (int i = 0; i < pieceList.Count; i++)
                        {
                            int rank = pieceList.GetPiece(i).Square.Rank;
                            // if pawn is pushed its worth more
                            whiteScore += (rank >= 3 && rank <= 7) ? 1 << (rank - 1) : 0;
                        }
                    }
                    else
                    {
                        blackScore += 100 * pieceList.Count;
                        // get the pawns square
                        for (int i = 0; i < pieceList.Count; i++)
                        {
                            int rank = pieceList.GetPiece(i).Square.Rank;
                            // if pawn is pushed its worth more
                            blackScore += (rank >= 2 && rank <= 6) ? 1 << (7 - rank) : 0;
                        }
                    }
                }
                else if (pieceList.TypeOfPieceInList == PieceType.Knight)
                { //knight
                    if (pieceList.IsWhitePieceList)
                    {
                        whiteScore += 300 * pieceList.Count;
                    }
                    else
                    {
                        blackScore += 300 * pieceList.Count;
                    }
                }
                else if (pieceList.TypeOfPieceInList == PieceType.Bishop)
                { //bishop
                    if (pieceList.IsWhitePieceList)
                    {
                        whiteScore += 300 * pieceList.Count;
                    }
                    else
                    {
                        blackScore += 300 * pieceList.Count;
                    }
                }
                else if (pieceList.TypeOfPieceInList == PieceType.Rook)
                { //rook
                    if (pieceList.IsWhitePieceList)
                    {
                        whiteScore += 500 * pieceList.Count;
                    }
                    else
                    {
                        blackScore += 500 * pieceList.Count;
                    }
                }
                else if (pieceList.TypeOfPieceInList == PieceType.Queen)
                { //queen
                    if (pieceList.IsWhitePieceList)
                    {
                        whiteScore += 900 * pieceList.Count;
                    }
                    else
                    {
                        blackScore += 900 * pieceList.Count;
                    }
                }
            }
            if (board.IsInCheck())
            {
                if (board.IsWhiteToMove)
                {
                    whiteScore -= 200;
                }
                else
                {
                    blackScore -= 200;
                }
            }
            if (rootIsWhite)
            {
                return whiteScore - blackScore;
            }
            else
            {
                return blackScore - whiteScore;
            }
        }
    }
}
