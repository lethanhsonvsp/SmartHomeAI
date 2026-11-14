function b_threshold(dataDir, outDir)
% NV09b: uoc luong nguong tu lan bat tay; so luat co dinh (28 C, 50 lux) voi luat thich nghi.
% Do: so phut (khi co nguoi o nha) luat lech hanh vi nguoi dung, va so lan chuyen trang thai
% cua luat ma nguoi dung khong dong y (bat/tat sai).
if ~exist(outDir, 'dir'), mkdir(outDir); end
S = load_csv(dataDir, 'sensor_reading'); C = load_csv(dataDir, 'device_command_log');
O = load_csv(dataDir, 'occupancy_log');
occ = state_at(O.ts, double(O.state == "Occupied"), S.ts); home = occ == 1;
cfg = {'fan', S.temp_c, 28, +1; 'light', S.lux, 50, -1};   % dir +1: bat khi x >= nguong
rows = {};
for k = 1:size(cfg, 1)
    [dev, x, fixedTh, dir] = cfg{k, :};
    m = C.device == dev & C.source == "manual" & C.on;
    xOn = interp1(datenum(S.ts), x, datenum(C.ts(m)), 'previous'); xOn = xOn(~isnan(xOn)); %#ok<DATNM>
    if isempty(xOn), warning('Khong co lan bat tay cua %s', dev); continue; end
    est = median(xOn);                      % nguong thich nghi = trung vi gia tri luc bat tay
    mAll = C.device == dev;
    user = state_at(C.ts(mAll), C.on(mAll), S.ts); user(isnan(user)) = 0;
    for r = {'co dinh', fixedTh; 'thich nghi', est}'
        ruleOn = dir * x >= dir * r{2};
        bad = home & (ruleOn ~= (user == 1));
        sw = find(diff(ruleOn) ~= 0) + 1; sw = sw(home(sw));   % lan chuyen cua luat khi co nguoi
        wrong = sum(ruleOn(sw) ~= (user(sw) == 1));
        rows(end+1, :) = {char(dev), r{1}, r{2}, sum(bad), 100*sum(bad)/max(1, sum(home)), numel(sw), wrong}; %#ok<AGROW>
    end
    f = figure('Visible', 'off'); histogram(xOn); xline(fixedTh, 'r--', 'co dinh'); xline(est, 'g-', 'thich nghi');
    title(sprintf('Gia tri luc bat tay: %s', dev)); saveas(f, fullfile(outDir, sprintf('b_%s_hist.png', dev))); close(f);
end
T = cell2table(rows, 'VariableNames', {'device','rule','threshold','mismatch_minutes','mismatch_pct','rule_switches','wrong_switches'});
writetable(T, fullfile(outDir, 'b_rules.csv')); disp(T);
f = figure('Visible', 'off'); bar(categorical(strcat(T.device, ' ', T.rule)), T.wrong_switches);
ylabel('So lan bat/tat sai'); title('Luat co dinh va thich nghi'); saveas(f, fullfile(outDir, 'b_wrong_switches.png')); close(f);
end
