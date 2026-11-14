function c_occupancy_eval(dataDir, truthFile, outDir)
% NV09c: so occupancy_log voi nhan that (cot ts, truth = home|away), mau moi phut.
% Du doan: AwayConfirmed = away; Occupied/OtherArea/ExitPending = home; Unknown bi loai (dem rieng).
if ~exist(outDir, 'dir'), mkdir(outDir); end
O = load_csv(dataDir, 'occupancy_log');
[p, n] = fileparts(truthFile); L = load_csv(p, n);
L.truth = string(L.truth);
t = (max(O.ts(1), L.ts(1)) : minutes(1) : min(O.ts(end), L.ts(end)) + hours(1))'; t.TimeZone = 'Asia/Ho_Chi_Minh';
tr = state_at(L.ts, double(L.truth == "away"), t);
st = string(O.state);
pr = state_at(O.ts, double(st == "AwayConfirmed") + 2 * double(st == "Unknown"), t);
ok = ~isnan(tr) & ~isnan(pr); unknown = sum(ok & pr == 2); ok = ok & pr ~= 2;
tr = tr(ok); pr = pr(ok);                       % 0 = home, 1 = away
cm = accumarray([tr pr] + 1, 1, [2 2]);         % hang: that, cot: du doan
acc = sum(diag(cm)) / sum(cm(:));
falseAway = cm(1, 2) / max(1, sum(cm(1, :)));   % bao vang sai / tong phut that su o nha
missedAway = cm(2, 1) / max(1, sum(cm(2, :)));
T = table(acc, falseAway, missedAway, unknown, sum(cm(:)), 'VariableNames', ...
    {'accuracy','false_away_rate','missed_away_rate','unknown_minutes','minutes_compared'});
writetable(T, fullfile(outDir, 'c_summary.csv'));
writetable(array2table(cm, 'VariableNames', {'pred_home','pred_away'}, 'RowNames', {'true_home','true_away'}), ...
    fullfile(outDir, 'c_confusion.csv'), 'WriteRowNames', true);
f = figure('Visible', 'off'); imagesc(cm); colorbar; axis equal tight;
set(gca, 'XTick', 1:2, 'XTickLabel', {'home','away'}, 'YTick', 1:2, 'YTickLabel', {'home','away'});
xlabel('Du doan'); ylabel('That'); title(sprintf('Nhan dien hien dien, accuracy %.3f', acc));
for i = 1:2, for j = 1:2, text(j, i, num2str(cm(i,j)), 'HorizontalAlignment', 'center', 'Color', 'w'); end, end
saveas(f, fullfile(outDir, 'c_confusion.png')); close(f);
disp(T);
end
