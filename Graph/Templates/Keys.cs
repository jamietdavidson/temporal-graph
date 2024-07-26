namespace Graph.Templates;


public static class ActionKey
{
    public static string ID { get; } = "id";
    public static string NAME { get; } = "name";
    public static string DESCRIPTION { get; } = "description";
    public static string PARTY { get; } = "party";
    public static string OBJECT_PROMISE { get; } = "object_promise";
    public static string OPERATION { get; } = "operation";
    public static string DEPENDS_ON { get; } = "depends_on";
    public static string SUPPORTING_INFO { get; } = "supporting_info";
    public static string STEPS { get; } = "steps";
    public static string THREAD_GROUP { get; } = "thread_group";
    public static string DEPENDENT_CHECKPOINTS { get; } = "dependent_checkpoints";
}

public static class ActionStepKey
{
    public static string TITLE { get; } = "title";
    public static string DESCRIPTION { get; } = "description";
}

public static class ActionOperationKey
{
    public static string INCLUDE { get; } = "include";
    public static string EXCLUDE { get; } = "exclude";
    public static string APPENDS_OBJECTS_TO { get; } = "appends_objects_to";
    public static string INCLUSION_TYPE { get; } = "inclusion_type";
}

public static class CheckpointKey
{
    public static string ID { get; } = "id";
    public static string ALIAS { get; } = "alias";
    public static string DESCRIPTION { get; } = "description";
    public static string ABBREVIATED_DESCRIPTION { get; } = "abbreviated_description";
    public static string GATE_TYPE { get; } = "gate_type";
    public static string SUPPORTING_INFO { get; } = "supporting_info";
    public static string CHECKPOINT_REFERENCES { get; } = "checkpoint_references";
    public static string DEPENDENT_CHECKPOINTS { get; } = "dependent_checkpoints";
    public static string DEPENDENCIES { get; } = "dependencies";
    public static string INPUT_ACTIONS { get; } = "input_actions";
    public static string INPUT_OBJECT_PROMISES { get; } = "input_object_promises";
    public static string THREAD_GROUP { get; } = "thread_group";
    public static string DEPENDENT_ACTIONS { get; } = "dependent_actions";
    public static string DEPENDENT_THREAD_GROUPS { get; } = "dependent_thread_groups";
    public static string OBJECT_PROMISE { get; } = "object_promise";
}

public static class DependencyKey
{
    public static string CHECKPOINT { get; } = "checkpoint";
    public static string OPERATOR { get; } = "operator";
    public static string LEFT_REFERENCE { get; } = "left_reference";
    public static string LEFT_TYPE { get; } = "left_type";
    public static string RIGHT_REFERENCE { get; } = "right_reference";
    public static string RIGHT_TYPE { get; } = "right_type";
    public static string LEFT { get; } = "left";
    public static string RIGHT { get; } = "right";
    public static string OPERAND_TYPE { get; } = "operand_type";
    public static string OPERAND_VALUE { get; } = "operand_value";
}

public static class ObjectPromiseKey
{
    public static string ID { get; } = "id";
    public static string NAME { get; } = "name";
    public static string DESCRIPTION { get; } = "description";
    public static string OBJECT_TYPE { get; } = "object_type";
    public static string REFERENCED_BY_ACTIONS { get; } = "referenced_by_actions";
    public static string DEPENDENT_CHECKPOINTS { get; } = "dependent_checkpoints";
}

public static class ObjectTypeKey
{
    public static string ID { get; } = "id";
    public static string NAME { get; } = "name";
    public static string DESCRIPTION { get; } = "description";
    public static string ATTRIBUTES { get; } = "attributes";
}

public static class ObjectTypeAttributeKey
{
    public static string NAME { get; } = "name";
    public static string DESCRIPTION { get; } = "description";
    public static string TYPE { get; } = "type";
    public static string OBJECT_TYPE { get; } = "object_type_attribute";
    public static string BOOLEAN { get; } = "boolean";
    public static string STRING { get; } = "string";
    public static string NUMERIC { get; } = "numeric";
    public static string BOOLEAN_LIST { get; } = "boolean_list";
    public static string STRING_LIST { get; } = "string_list";
    public static string NUMERIC_LIST { get; } = "numeric_list";
    public static string EDGE { get; } = "edge";
    public static string EDGE_COLLECTION { get; } = "edge_collection";
}

public static class TermKey
{
    public static string NAME { get; } = "name";
    public static string DESCRIPTION { get; } = "description";
    public static string ATTRIBUTES { get; } = "attributes";
}


public static class ThreadGroupKey
{
    public static string ID { get; } = "id";
    public static string NAME { get; } = "name";
    public static string DESCRIPTION { get; } = "description";
    public static string SPAWN_FOREACH { get; } = "spawn_foreach";
    public static string SPAWN_AS { get; } = "spawn_as";
    public static string THREAD_GROUP { get; } = "thread_group";
    public static string DEPENDS_ON { get; } = "depends_on";
    public static string THREADED_ACTIONS { get; } = "threaded_actions";
    public static string NESTED_THREAD_GROUPS { get; } = "nested_thread_groups";
}
