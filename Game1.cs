using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace _1DChess;

// TODO: add a play again and exit screen
// TODO: display checkmate or stalemate on screen
// TODO: bug test

public static class Globals
{
    public const int WINDOWHEIGHT = 96;
    public const int WINDOWWIDTH = 768; 

    public static readonly int[,] MINMAXTILEPOSX = {
        {0, WINDOWWIDTH / 8, WINDOWWIDTH / 8 * 2, WINDOWWIDTH / 8 * 3, WINDOWWIDTH / 8 * 4, WINDOWWIDTH / 8 * 5, WINDOWWIDTH / 8 * 6, WINDOWWIDTH / 8 * 7},
        {WINDOWWIDTH / 8, WINDOWWIDTH / 8 * 2, WINDOWWIDTH / 8 * 3, WINDOWWIDTH / 8 * 4, WINDOWWIDTH / 8 * 5, WINDOWWIDTH / 8 * 6, WINDOWWIDTH / 8 * 7, WINDOWWIDTH / 8 * 8}
    };
}

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    
    public MouseState _mouseState;
    public MouseState _previousMouseState;
    
    public string selectedPiece = "NaP";
    public int selectedPieceIndex = -1;
    private bool isPieceSelected = false; 
    
    private bool capturedAPiece = false;

    private bool inCheckByKnight = false;
    private bool inCheckByRook = false;
    private List<(int from, int to)> inCheckValidMoves = new List<(int from, int to)>();

    public string[] board = ["wl", "wk", "wr", "NaP", "NaP", "br", "bk", "bl"];

    public int turn = 1;

    private Texture2D _background;
    private Rectangle _backgroundSize;

    private ChessPiece whiteKing;
    private ChessPiece whiteKnight;
    private ChessPiece whiteRook;

    private ChessPiece blackKing;
    private ChessPiece blackKnight;
    private ChessPiece blackRook;

    private SpriteBatch _spriteBatch;

    Song movePiece;
    Song capturePiece;
    Song checkKing;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _graphics.IsFullScreen = false;
        _graphics.PreferredBackBufferWidth = Globals.WINDOWWIDTH;
        _graphics.PreferredBackBufferHeight = Globals.WINDOWHEIGHT;
        _graphics.ApplyChanges();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _background = Content.Load<Texture2D>("chess-board");
        _backgroundSize = new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        whiteKing = new ChessPiece(Content.Load<Texture2D>("w-king"));
        whiteKing.SetLocation(0);
        whiteKnight = new ChessPiece(Content.Load<Texture2D>("w-knight"));
        whiteKnight.SetLocation(1);
        whiteRook = new ChessPiece(Content.Load<Texture2D>("w-rook"));
        whiteRook.SetLocation(2);

        blackKing = new ChessPiece(Content.Load<Texture2D>("b-king"));
        blackKing.SetLocation(7);
        blackKnight = new ChessPiece(Content.Load<Texture2D>("b-knight"));
        blackKnight.SetLocation(6);
        blackRook = new ChessPiece(Content.Load<Texture2D>("b-rook"));
        blackRook.SetLocation(5);

        this.movePiece = Content.Load<Song>("move-self");
        this.capturePiece = Content.Load<Song>("capture");
        this.checkKing = Content.Load<Song>("move-check");

    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        _mouseState = Mouse.GetState();
        if (turn % 2 != 0)
        {
            HandleMouseInput();
        }
        else
        {
            ComputerTurn();
        }
        UpdateBoardPositions();

        _previousMouseState = _mouseState;

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _spriteBatch.Begin();

        _spriteBatch.Draw(_background, _backgroundSize, Color.White);

        whiteKing.Draw(_spriteBatch);
        whiteKnight.Draw(_spriteBatch);
        whiteRook.Draw(_spriteBatch);

        blackKing.Draw(_spriteBatch);
        blackKnight.Draw(_spriteBatch);
        blackRook.Draw(_spriteBatch);

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    public void UpdateBoardPositions()
    {
        whiteKing.SetLocation(Array.IndexOf(board, "wl"));
        whiteKnight.SetLocation(Array.IndexOf(board, "wk"));
        whiteRook.SetLocation(Array.IndexOf(board, "wr"));

        blackKing.SetLocation(Array.IndexOf(board, "bl"));
        blackKnight.SetLocation(Array.IndexOf(board, "bk"));
        blackRook.SetLocation(Array.IndexOf(board, "br"));
    }

    public void PlaySound(Song sound)
    {
        MediaPlayer.Play(sound);
        MediaPlayer.IsRepeating = false;
    }

    public void HandleMouseInput()
    {   
        if (Stalemate(board)){
            Console.WriteLine("STALEMATE");
            Exit();
        }
        else if (_previousMouseState.LeftButton == ButtonState.Released && _mouseState.LeftButton == ButtonState.Pressed && _mouseState.Y > 0 && _mouseState.Y < Globals.WINDOWHEIGHT && _mouseState.X > 0 && _mouseState.X < Globals.WINDOWWIDTH)
        {
            for (int i = 0; i < Globals.MINMAXTILEPOSX.GetLength(1); i++)
            {
                int tileMin = Globals.MINMAXTILEPOSX[0, i];
                int tileMax = Globals.MINMAXTILEPOSX[1, i];

                if (_mouseState.X > tileMin && _mouseState.X < tileMax)
                {   
                    if (board[i].StartsWith("w"))
                    {
                        selectedPiece = board[i];
                        selectedPieceIndex = i;
                        isPieceSelected = true;
                    }
                    else
                    {
                        if (!Check(board, "wl"))
                        {
                            if (isPieceSelected && ValidMove(board, selectedPiece, selectedPieceIndex, i))
                            {
                                if (board[i] != "NaP")
                                {
                                    capturedAPiece = true;
                                }
                                
                                board[selectedPieceIndex] = "NaP";
                                board[i] = selectedPiece;

                                if (Check(board, "bl"))
                                {
                                    PlaySound(checkKing);
                                    capturedAPiece = false;
                                }
                                if (capturedAPiece)
                                {
                                    PlaySound(capturePiece);
                                    capturedAPiece = false;
                                }
                                else
                                {
                                    PlaySound(movePiece);
                                }

                                turn++;

                                selectedPiece = "NaP";
                                selectedPieceIndex = -1;
                                isPieceSelected = false;
                            }
                        }
                        else
                        {
                            InCheckMoves(board, "wl");

                            Console.WriteLine("WHITE IN CHECK");
                            
                            if (inCheckValidMoves.Count > 0)
                            {
                                if (isPieceSelected && inCheckValidMoves.Contains((selectedPieceIndex, i)))
                                {
                                    if (board[i] != "NaP")
                                    {
                                        capturedAPiece = true;
                                    }
                                    
                                    board[selectedPieceIndex] = "NaP";
                                    board[i] = selectedPiece;

                                    if (Check(board, "bl"))
                                    {
                                        PlaySound(checkKing);
                                        capturedAPiece = false;
                                    }
                                    if (capturedAPiece)
                                    {
                                        PlaySound(capturePiece);
                                        capturedAPiece = false;
                                    }
                                    else
                                    {
                                        PlaySound(movePiece);
                                    }

                                    turn++;

                                    selectedPiece = "NaP";
                                    selectedPieceIndex = -1;
                                    isPieceSelected = false;
                                }
                            }
                            else
                            {
                                Console.WriteLine("CHECKMATE");
                                Exit();
                            }
                        }
                    }
                }
            }
        }
    }

    public void ComputerTurn()
    {
        if (Stalemate(board))
        {
            Console.WriteLine("STALEMATE");
            Exit();
        }
        else
        {
            Thread.Sleep(2000);
            var validMoves = new List<(int from, int to)>();

            if (!Check(board, "bl"))
            {
                for (int i = 0; i < board.Length; i++)
                {
                    if (board[i].StartsWith("b"))
                    {
                        for (int j = 0; j < board.Length; j++)
                        {
                            if (i != j && ValidMove(board, board[i], i, j))
                            {
                                validMoves.Add((i, j));
                            }
                        }
                    }
                }

                if (validMoves.Count > 0)
                {
                    Random random = new Random();
                    var move = validMoves[random.Next(validMoves.Count)];

                    if (board[move.to] != "NaP")
                    {
                        capturedAPiece = true;
                    }

                    board[move.to] = board[move.from];
                    board[move.from] = "NaP";

                    if (Check(board, "wl"))
                    {
                        PlaySound(checkKing);
                        capturedAPiece = false;
                    }
                    else if (capturedAPiece)
                    {
                        PlaySound(capturePiece);
                        capturedAPiece = false;
                    }
                    else
                    {
                        PlaySound(movePiece);
                    }

                    turn++;
                }
                else
                {
                    Console.WriteLine("STALEMATE");
                    Exit();
                }
            }
            else
            {
                InCheckMoves(board, "bl");

                Console.WriteLine("BLACK IN CHECK");

                Random random = new Random();

                if (inCheckValidMoves.Count > 0)
                {
                    var move = inCheckValidMoves[random.Next(inCheckValidMoves.Count)];

                    if (board[move.to] != "NaP")
                    {
                        capturedAPiece = true;
                    }

                    board[move.to] = board[move.from];
                    board[move.from] = "NaP";

                    if (Check(board, "wl"))
                    {
                        PlaySound(checkKing);
                        capturedAPiece = false;
                    }
                    else if (capturedAPiece)
                    {
                        PlaySound(capturePiece);
                        capturedAPiece = false;
                    }
                    else
                    {
                        PlaySound(movePiece);
                    }

                    turn++;
                }
                else
                {
                    Console.WriteLine("CHECKMATE");
                    Exit();
                }
            }
        }
    }

    public static bool ValidMove(string[] board, string selectedPiece, int pieceIndex, int moveIndex)
    {
        if (selectedPiece.Contains("l"))
        {
                if (pieceIndex + 1 == moveIndex || pieceIndex - 1 == moveIndex) {
                    if (!board[moveIndex].StartsWith(selectedPiece[0]))
                    {
                        return true;
                    }
                    return false;
                }
               return false;
        }
        else if (selectedPiece.Contains("k"))
        {
            if (pieceIndex + 2 == moveIndex || pieceIndex - 2 == moveIndex)
            {
                if (!board[moveIndex].StartsWith(selectedPiece[0]))
                {
                    return true;
                }
                return false;
            }
            return false;
        }
        else if (selectedPiece.Contains("r"))
        {
            if (!board[moveIndex].StartsWith(selectedPiece[0]))
            {
                if (pieceIndex > moveIndex)
                {
                    for(int i = moveIndex + 1; i < pieceIndex; i++)
                    {
                        if (board[i] != "NaP")
                        {
                            return false;
                        }
                    }
                    return true;
                }
                else
                {
                    for(int i = pieceIndex + 1; i < moveIndex; i++)
                    {
                        if (board[i] != "NaP")
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }
            return false;
        }
        else
        {
            return false;
        }
    }

    public bool Check(string[] board, string selectedKing)
    {
        for (int i = 0; i < board.Length; i++)
        {
            if (board[i] == selectedKing)
            {
                for(int j = 0; j < board.Length; j++)
                {
                    if(!board[j].Contains("NaP") && !board[j].Contains(selectedKing[0]))
                    {
                        if(board[j].Contains("l"))
                        {
                            if (j + 1 == i || j - 1 == i)
                            {
                                return true;
                            }
                        }
                        else if (board[j].Contains("k"))
                        {
                            if (j + 2 == i || j - 2 == i)
                            {
                                inCheckByKnight = true;
                                return true;
                            }
                        }
                        else if (board[j].Contains("r"))
                        {
                            if (j > i)
                            {
                                for(int k = i + 1; k < j; k++)
                                {
                                    if (board[k] != "NaP")
                                    {
                                        return false;
                                    }
                                }
                                inCheckByRook = true;
                                return true;
                            }
                            else
                            {
                                for(int k = j + 1; k < i; k++)
                                {
                                    if (board[k] != "NaP")
                                    {
                                        return false;
                                    }
                                }
                                inCheckByRook = true;
                                return true;
                            }
                        }
                    }
                }
            }
        }
        return false;
    }

    public void InCheckMoves(string[] board, string selectedKing)
    {
        inCheckValidMoves.Clear();
        if (inCheckByRook)
        {
            for (int i = 0; i < board.Length; i++)
            {
                if (board[i].Contains("r") && !board[i].StartsWith(selectedKing[0]))
                {
                    for (int j = 0; j < board.Length; j++)
                    {
                        if (board[j].Contains("r") && board[j].StartsWith(selectedKing[0]))
                        {
                            if (ValidMove(board, board[j], j, i))
                            {
                                inCheckValidMoves.Add((j, i));
                            }
                        }
                        else if (board[j].Contains("k") && board[j].StartsWith(selectedKing[0]))
                        {
                            if (ValidMove(board, board[j], j, i))
                            {
                                inCheckValidMoves.Add((j, i));
                            }
                        }
                        else if (board[j].Contains("l") && board[j].StartsWith(selectedKing[0]))
                        {
                            if (ValidMove(board, board[j], j, i))
                            {
                                inCheckValidMoves.Add((j, i));
                            }
                        }
                    }
                }
            }
        }
        else if (inCheckByKnight)
        {
            for (int i = 0; i < board.Length; i++)
            {
                if (board[i].Contains("r") && !board[i].StartsWith(selectedKing[0]))
                {
                    for (int j = 0; j < board.Length; j++)
                    {
                        if (board[j].Contains("r") && board[j].StartsWith(selectedKing[0]))
                        {
                            if (ValidMove(board, board[j], j, i))
                            {
                                inCheckValidMoves.Add((j, i));
                            }
                        }
                        else if (board[j].Contains("k") && board[j].StartsWith(selectedKing[0]))
                        {
                            if (ValidMove(board, board[j], j, i))
                            {
                                inCheckValidMoves.Add((j, i));
                            }
                        }
                        else if (board[j].Contains("l") && board[j].StartsWith(selectedKing[0]))
                        {
                            if (ValidMove(board, board[j], j, i))
                            {
                                inCheckValidMoves.Add((j, i));
                            }
                            else
                            {
                                inCheckValidMoves.Add((j, i+1));
                                if (i-1 > 0)
                                {
                                    inCheckValidMoves.Add((j, i-1));
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public bool Stalemate(string[] board)
    {
        if (board[0] == "wl" && board[1] == "wk" && board[2] == "br")
        {
            return true;
        }
        else if (board[0] == "wl" && board[1] == "wk" && board[2] == "bk" && board[4] != "wr" && board[3] != "wr")
        {
            return true;
        }
        else if (board[7] == "bl" && board[6] == "bk" && board[5] == "wr")
        {
            return true;
        }
        else if (board[7] == "bl" && board[6] == "bk" && board[5] == "wk" && board[4] != "br" && board[3] != "br")
        {
            return true;
        }
        else if (board[6] == "bl" && board[5] == "wk" && board[4] == "wr")
        {
            return true;
        }
        else if (board[2] == "wl" && board[3] == "bk" && board[4] == "br")
        {
            return true;
        }
        else
        {
            return false;
        }
    }

}

public class ChessPiece()
{
    public int location;
    public Vector2 position;
    public Vector2 origin = new Vector2(0, 0);
    private Texture2D texture;
    float scale = 0.15f;

    public ChessPiece(Texture2D texture) : this()
    {
        this.texture = texture;
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        int positionX = Globals.WINDOWWIDTH / 8 * location + ((Globals.WINDOWWIDTH / 8) / 4);
        position = new Vector2(positionX, Globals.WINDOWHEIGHT / 4);
    }

    public void SetLocation(int newLocation)
    {
        location = newLocation;
        UpdatePosition();
    }

    public void Draw(SpriteBatch cp)
    {
        cp.Draw(texture, position, null, Color.White, 0, origin, scale, SpriteEffects.None, 0);
    }
}