namespace CodeExplorer.Cypher.Ast;

public enum Direction
{
    Outgoing,   // -[:REL]->
    Incoming,   // <-[:REL]-
    Undirected  // -[:REL]-
}
