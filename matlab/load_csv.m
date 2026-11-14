function T = load_csv(dataDir, name)
% Doc <dataDir>/<name>.csv; cot thoi gian doi sang datetime gio Viet Nam.
T = readtable(fullfile(dataDir, [name '.csv']), 'TextType', 'string');
for c = ["on" "open"]   % true/false dang chuoi -> logical
    if ismember(c, T.Properties.VariableNames) && ~islogical(T.(c)), T.(c) = string(T.(c)) == "true"; end
end
for c = ["ts" "raised_at" "cancelled_at"]
    if ismember(c, T.Properties.VariableNames)
        v = T.(c);
        if isstring(v) || iscellstr(v)
            v = datetime(string(v), 'InputFormat', 'yyyy-MM-dd''T''HH:mm:ss''Z''', 'TimeZone', 'UTC');
        end
        v.TimeZone = 'Asia/Ho_Chi_Minh';
        T.(c) = v;
    end
end
end
