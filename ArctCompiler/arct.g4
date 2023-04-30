grammar arct;
program:
        constant?
        function? 
        main;
     
main: type 'main()' block;

block: LBRACE (statement)* RBRACE;

constant: CONST variable (ENDPOINT CONST variable)* ENDPOINT;

variable: type identifier( EQ expression )?;
                            
function: FUNC type identifier LRBRACKET parameter RRBRACKET block;
                                           
type
    : 'int'
    | 'double'
    ;

parameter: (variable(COMMA variable)*)?;

statement
    : assignmentStatement
    | coutStatement     ENDPOINT
    | ifStatement
    | whileStatement
    | continueStatement ENDPOINT
    | expression        ENDPOINT
    | callFunction      ENDPOINT
    | constant
    | variable          ENDPOINT
    | returnStatement   ENDPOINT
    ;
    
callFunction: identifier LRBRACKET expressionUnion RRBRACKET;

assignmentStatement: identifier EQ expression;

coutStatement: COUT LRBRACKET (expressionUnion | '"'STRING'"') RRBRACKET;

expressionUnion: (expression(COMMA expression)*)?;

ifStatement: IF LRBRACKET conditionUnion RRBRACKET block (ELSE block)?;

whileStatement: WHILE LRBRACKET conditionUnion RRBRACKET block;

returnStatement: RETURN factor;

continueStatement: CONTIN;

condition
    : expression 
    | callFunction
    | expression operations expression
    ;

conditionUnion: condition (op = ('and'|'or') condition)*;

expression 
    : factor    #expressionFactor
    | expression plusminus expression #expressionAdd
    | expression multdivmod expression #expressionMul
    | '<'type'>' expression #expressionConvert
    ;


factor
    : identifier  
    | INTEGER
    | DOUBLE
    | LRBRACKET factor RRBRACKET
    | assignmentStatement
    | callFunction
    ;

identifier: STRING(STRING|NUMBER)*;
                                               
operations
    : EQQ
    | NEG
    | BIGGER
    | EQBIGGER
    | LESS
    | EQLESS
    ;                  
plusminus
    : PLUS
    | MINUS
    ; 
multdivmod
    : MULT
    | DIV
    | MOD
    ;

IF:           'if';
WHILE:        'while';
RETURN:       'return';
FUNC:         'func';
COUT:         'cout';
DOUBLE:       NUMBER'.'NUMBER*; 
INTEGER:      NUMBER;
STRING:       [a-zA-Z][a-zA-Z]*;
NUMBER:       [0-9][0-9]*;
CONTIN:       'continue';
BREAK:        'break';
CONST:        'const';
ELSE:         'else';
AND:          'and';
OR:           'or';
COMMA:        ',';
EQ:           '=';
ENDPOINT:     ';';
LBRACE:       '{';   
RBRACE:       '}';
LRBRACKET:    '(';   
RRBRACKET:    ')';
COMMENT:      '//' ~[\r\n]* -> skip;
WS:           [ \t\r\n]     -> skip;
BLOCKCOMMENT: '/*'.*?'*/'   -> skip;

// LOGIC OPERATIONS
EQQ:      '==';
NEG:      '!=';
BIGGER:   '>';
EQBIGGER: '>=';
LESS:     '<';
EQLESS:   '<=';
// MATH OPERATIONS
PLUS:  '+';
MINUS: '-';
MULT:  '*';
DIV:   '/';
MOD:   '%';
