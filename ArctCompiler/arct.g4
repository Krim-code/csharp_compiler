grammar arct;

program: function*;

function: functionHead LBRACE functionBody RBRACE;

functionHead: type ID LPAREN arguments RPAREN;

arg: type ID;
arguments: arg? (',' arg)*;

functionBody: (statement)*;

statement
    : assignmentStatement
    | returnStatement
    | ifStatement
    | whileStatement
    | printStatement
    | moveValueVariable
    ;


assignmentStatement: type ID EQ expression END_STATE;

returnStatement: 'return' expression END_STATE;

whileStatement:  WHILE LPAREN equation RPAREN LBRACE (statement|expression)* RBRACE;


convert_type: '<<' type '>>' LPAREN expression RPAREN;

moveValueVariable:ID EQ expression END_STATE;

printStatement :'print' LPAREN (STRING|atom) RPAREN END_STATE;

ifStatement: 'if' LPAREN equation RPAREN LBRACE (statement)* RBRACE;

expression

   :  convert_type                            #expressionToType
   |  functionCall                            #expressionFunctionCall
   |  expression op=POW expression            #expressionPow
   |  expression op=(TIMES | DIV| REM)  expression #expressionMul
   |  expression op=(PLUS | MINUS) expression #expressionAdd
   |  LPAREN expression RPAREN                #expressionNested
   |  (PLUS | MINUS)* atom                    #expressionNumber
   |  STRING                                  #expressionString
   ;


equation
   : expression op=relop expression
   ;

relop
   : EQ_EQ
   | GT
   | LT
   | NOT_EQ
   ;

param: expression;
params: param? (',' param)*;

functionCall: ID LPAREN params RPAREN;


atom
    : op=INT
    | op=DECIMAL
    | op=ID
    ;

type
    : 'int'
    | 'double'
    ;

WHILE: 'while';
STRING : '"' (ESC | ~["\\])* '"';
fragment ESC: '\\' .;

ID: [a-zA-Z]+ DIGIT*;
TIMES: '*';
REM: '%';
DIV: '/';
PLUS: '+';
MINUS: '-';
LPAREN: '(';
RPAREN: ')';
POW: '^';
EQ: '=';
EQ_EQ: '==';
NOT_EQ: '!=';
LBRACE: '{';
RBRACE: '}';
END_STATE:';';
GT: '>';
LT: '<';

INT: DIGIT+;
DECIMAL: INT '.' INT;

fragment DIGIT: [0-9];
fragment SIGN: (MINUS)?;


WS
   : [ \r\n\t] + -> skip
   ;


COMMENT
    :   '/*' .*? '*/' -> skip
    ;

LINE_COMMENT
    :   '//' ~[\r\n]* -> skip
    ;